using VoiceToPaste.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Serilog;

namespace VoiceToPaste.Services
{
    /// <summary>
    /// Odczyt i zapis ustawień w pliku settings.yaml obok pliku wykonywalnego.
    /// Brak pliku albo uszkodzona zawartość nigdy nie wywalają aplikacji — wracamy
    /// do wartości domyślnych, naprawiamy plik, a szczegóły trafiają do LastLoadDiagnostic.
    /// </summary>
    public sealed class SettingsService
    {
        private static readonly ILogger Logger = Log.ForContext<SettingsService>();
        private const string SettingsFileName = "settings.yaml";

        public SettingsService()
            : this(ApplicationPaths.Directory)
        {
        }

        // Katalog podany jawnie — używany przez testy, żeby nie dotykać katalogu aplikacji.
        public SettingsService(string settingsDirectory)
        {
            SettingsPath = Path.Combine(settingsDirectory, SettingsFileName);
        }

        public string SettingsPath { get; }

        /// <summary>Komunikat diagnostyczny z ostatniego odczytu; null, gdy odczyt był czysty.</summary>
        public string? LastLoadDiagnostic { get; private set; }

        public AppSettings Load()
        {
            Logger.Information("Starting settings load.");
            LastLoadDiagnostic = null;

            if (!File.Exists(SettingsPath))
            {
                Logger.Warning("Settings file was not found. Default values will be restored.");
                return RestoreDefaults("Brak pliku settings.yaml — utworzono go z ustawieniami domyślnymi.");
            }

            try
            {
                var yaml = File.ReadAllText(SettingsPath);
                var settings = CreateDeserializer().Deserialize<AppSettings>(yaml);

                // Pusty plik to poprawny YAML bez treści — traktujemy go jak brak ustawień.
                if (settings == null)
                {
                    Logger.Warning("Settings file is empty. Default values will be restored.");
                    return RestoreDefaults("Plik settings.yaml był pusty — przywrócono ustawienia domyślne.");
                }

                var repairs = new List<string>();

                // Jawny wpis keyWords: null w pliku nie może wysypać edytora ani późniejszej podmiany.
                settings.KeyWords ??= [];
                if (settings.WhisperModelId != null && WhisperModelCatalog.TryGetById(settings.WhisperModelId) == null)
                {
                    Logger.Warning("Invalid Whisper model identifier {ModelId}. Model selection will be disabled.",
                        settings.WhisperModelId);
                    settings.WhisperModelId = null;
                    repairs.Add("Nieprawidłowy model Whisper — wyłączono wybór modelu.");
                }

                var normalizedLanguage = TranscriptionLanguages.Normalize(settings.TranscribeLanguage);
                if (!string.Equals(settings.TranscribeLanguage, normalizedLanguage, StringComparison.Ordinal))
                {
                    Logger.Warning("Invalid transcription language code {LanguageCode}. {NormalizedLanguageCode} will be used.",
                        settings.TranscribeLanguage,
                        normalizedLanguage);
                    settings.TranscribeLanguage = normalizedLanguage;
                    repairs.Add("Nieprawidłowy język transkrypcji — przywrócono bezpieczną wartość domyślną.");
                }

                var normalizedUiLanguage = UiLanguages.Normalize(settings.UiLanguage);
                if (!string.Equals(settings.UiLanguage, normalizedUiLanguage, StringComparison.Ordinal))
                {
                    Logger.Warning("Invalid UI language code {LanguageCode}. {NormalizedLanguageCode} will be used.",
                        settings.UiLanguage,
                        normalizedUiLanguage);
                    settings.UiLanguage = normalizedUiLanguage;
                    repairs.Add("Nieprawidłowy język interfejsu — przywrócono bezpieczną wartość domyślną.");
                }

                if (settings.Hotkey != null && !settings.Hotkey.IsValid(out _))
                {
                    Logger.Warning("Invalid global hotkey. The default hotkey will be restored.");
                    settings.Hotkey = HotkeyGesture.CreateDefault();
                    repairs.Add("Nieprawidłowy skrót globalny — przywrócono Ctrl + Space.");
                }

                if (settings.RecordingLimitSeconds < AppSettings.MinimumRecordingLimitSeconds ||
                    settings.RecordingLimitSeconds > AppSettings.MaximumRecordingLimitSeconds)
                {
                    Logger.Warning(
                        "Invalid recording limit {RecordingLimitSeconds}. {DefaultRecordingLimitSeconds} seconds will be used.",
                        settings.RecordingLimitSeconds,
                        AppSettings.DefaultRecordingLimitSeconds);
                    settings.RecordingLimitSeconds = AppSettings.DefaultRecordingLimitSeconds;
                    repairs.Add("Nieprawidłowy limit nagrywania — przywrócono 60 sekund.");
                }

                if (repairs.Count > 0)
                    RepairSettings(settings, string.Join(" ", repairs));

                Logger.Information("Loaded settings with backend {Backend}.", settings.TranscriptionEngine);
                return settings;
            }
            catch (Exception ex) when (ex is YamlException or IOException or UnauthorizedAccessException)
            {
                Logger.Warning(ex, "Failed to load settings. Default values will be restored.");
                return RestoreDefaults(
                    $"Nie udało się odczytać settings.yaml ({ex.Message}) — przywrócono ustawienia domyślne.");
            }
        }

        /// <summary>
        /// Zapisuje ustawienia atomowo: najpierw plik tymczasowy obok, potem jego podmiana
        /// za właściwy plik. Awaria w połowie zapisu nie zostawi więc uciętego settings.yaml.
        /// </summary>
        public void Save(AppSettings settings)
        {
            Logger.Information("Saving settings with backend {Backend}.", settings.TranscriptionEngine);
            try
            {
                var yaml = CreateSerializer().Serialize(settings);
                var tempPath = SettingsPath + ".tmp";

                // Plik tymczasowy leży w tym samym katalogu, więc podmiana odbywa się
                // w obrębie jednego wolumenu i jest atomowa.
                File.WriteAllText(tempPath, yaml);
                File.Move(tempPath, SettingsPath, overwrite: true);
                Logger.Information("Settings were saved.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to save settings.");
                throw;
            }
        }

        /// <summary>
        /// Zwraca ustawienia domyślne i próbuje naprawić plik na dysku, żeby użytkownik miał
        /// poprawny punkt startu do ręcznej edycji. Błąd zapisu naprawczego jest tylko
        /// dopisywany do diagnostyki — odczyt nie może się na nim wyłożyć.
        /// </summary>
        private AppSettings RestoreDefaults(string diagnostic)
        {
            var defaults = new AppSettings();

            RepairSettings(defaults, diagnostic);
            return defaults;
        }

        private void RepairSettings(AppSettings settings, string diagnostic)
        {
            try
            {
                Logger.Information("Saving repaired settings.");
                Save(settings);
                LastLoadDiagnostic = diagnostic;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Error(ex, "Failed to save default settings.");
                LastLoadDiagnostic = $"{diagnostic} Nie udało się zapisać pliku: {ex.Message}";
            }
        }

        private static ISerializer CreateSerializer() =>
            new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

        private static IDeserializer CreateDeserializer() =>
            new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithEnumNamingConvention(CamelCaseNamingConvention.Instance)
                // Nieznane klucze (np. z nowszej wersji aplikacji) nie mogą wysypać odczytu.
                .IgnoreUnmatchedProperties()
                .Build();
    }
}
