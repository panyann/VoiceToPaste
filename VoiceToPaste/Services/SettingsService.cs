using VoiceToPaste.Models;
using VoiceToPaste.Resources;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Serilog;

namespace VoiceToPaste.Services
{
    /// <summary>
    /// Reads and writes settings in the settings.yaml file next to the executable.
    /// A missing or corrupted file never crashes the application — defaults are
    /// restored, the file is repaired, and details are reported in LastLoadDiagnostic.
    /// </summary>
    public sealed class SettingsService
    {
        private static readonly ILogger Logger = Log.ForContext<SettingsService>();
        private const string SettingsFileName = "settings.yaml";
        private AppSettings? _settings;

        public SettingsService()
            : this(ApplicationPaths.Directory)
        {
        }

        // Directory provided explicitly — used by tests so the application directory is untouched.
        public SettingsService(string settingsDirectory)
        {
            SettingsPath = Path.Combine(settingsDirectory, SettingsFileName);
        }

        public string SettingsPath { get; }

        /// <summary>Diagnostic message from the most recent load; null when the load was clean.</summary>
        public string? LastLoadDiagnostic { get; private set; }

        /// <summary>
        /// The process-wide settings — a single instance mutated live by consumers and
        /// flushed to disk by the parameterless Save. Throws before the first Load,
        /// because silent defaults would be worse than an explicit error.
        /// </summary>
        public AppSettings Settings
        {
            get
            {
                if (_settings == null)
                    throw LocalizedExceptionFactory.InvalidOperation("SettingsNotLoaded");

                return _settings;
            }
        }

        /// <summary>
        /// Reads settings from disk exactly once per process. A second call throws,
        /// because it would replace the instance and orphan references previously
        /// captured by consumers.
        /// </summary>
        public AppSettings Load()
        {
            if (_settings != null)
                throw LocalizedExceptionFactory.InvalidOperation("SettingsAlreadyLoaded");

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

                // An empty file is valid YAML with no content — treat it as missing settings.
                if (settings == null)
                {
                    Logger.Warning("Settings file is empty. Default values will be restored.");
                    return RestoreDefaults("Plik settings.yaml był pusty — przywrócono ustawienia domyślne.");
                }

                // The instance must be reachable through Settings before any repair save runs.
                _settings = settings;

                var repairs = new List<string>();

                // An explicit keyWords: null entry must not crash the editor or later replacement.
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

                // The theme is a plain string, so an unknown value repairs only this field
                // instead of resetting the whole file like a broken enum would.
                var normalizedTheme = AppThemes.Normalize(settings.SelectedTheme);
                if (!string.Equals(settings.SelectedTheme, normalizedTheme, StringComparison.Ordinal))
                {
                    Logger.Warning("Invalid selected theme {SelectedTheme}. {NormalizedTheme} will be used.",
                        settings.SelectedTheme,
                        normalizedTheme);
                    settings.SelectedTheme = normalizedTheme;
                    repairs.Add("Nieprawidłowy motyw interfejsu — przywrócono bezpieczną wartość.");
                }

                if (settings.Hotkey != null && !settings.Hotkey.IsValid(out _))
                {
                    Logger.Warning("Invalid global hotkey. The default hotkey will be restored.");
                    settings.Hotkey = HotkeyGesture.CreateDefault();
                    repairs.Add("Nieprawidłowy skrót globalny — przywrócono Ctrl + Shift + Space.");
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
                    RepairSettings(string.Join(" ", repairs));

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
        /// Atomically writes the current Settings: first a temp file next to it, then a
        /// move over the real file. A failure mid-write therefore never leaves a truncated
        /// settings.yaml.
        /// </summary>
        public void Save()
        {
            var settings = Settings;
            Logger.Information("Saving settings with backend {Backend}.", settings.TranscriptionEngine);
            try
            {
                var yaml = CreateSerializer().Serialize(settings);
                var tempPath = SettingsPath + ".tmp";

                // The temp file lives in the same directory, so the move stays within one
                // volume and is atomic.
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
        /// Returns default settings and tries to repair the file on disk so the user has a
        /// valid starting point for manual editing. A repair write error is only appended to
        /// the diagnostics — the load must not fail on it.
        /// </summary>
        private AppSettings RestoreDefaults(string diagnostic)
        {
            var defaults = new AppSettings();

            _settings = defaults;
            RepairSettings(diagnostic);
            return defaults;
        }

        private void RepairSettings(string diagnostic)
        {
            try
            {
                Logger.Information("Saving repaired settings.");
                Save();
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
                // Unknown keys (e.g. from a newer app version) must not break the load.
                .IgnoreUnmatchedProperties()
                .Build();
    }
}
