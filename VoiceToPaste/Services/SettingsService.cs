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
    /// restored, the file is repaired, and repair details are written to the log.
    /// </summary>
    public sealed class SettingsService
    {
        private static readonly ILogger Logger = Log.ForContext<SettingsService>();
        private const string SettingsFileName = "settings.yaml";
        private AppSettings? _settings;

        // Set by the repair methods after an actual change, so the file is written again
        // only when a repair really happened. Reset at the start of each repair pass.
        private bool _settingsChanged;

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

            if (!File.Exists(SettingsPath))
            {
                Logger.Warning("Settings file was not found. Default values will be restored.");
                return RestoreDefaults();
            }

            try
            {
                var yaml = File.ReadAllText(SettingsPath);
                var settings = CreateDeserializer().Deserialize<AppSettings>(yaml);

                // An empty file is valid YAML with no content — treat it as missing settings.
                if (settings == null)
                {
                    Logger.Warning("Settings file is empty. Default values will be restored.");
                    return RestoreDefaults();
                }

                // The instance must be reachable through Settings before any repair save runs.
                _settings = settings;

                RepairLoadedSettings(settings);

                Logger.Information("Loaded settings with backend {Backend}.", settings.TranscriptionEngine);
                return settings;
            }
            catch (Exception ex) when (ex is YamlException or IOException or UnauthorizedAccessException)
            {
                Logger.Warning(ex, "Failed to load settings. Default values will be restored.");
                return RestoreDefaults();
            }
        }

        /// <summary>
        /// Repairs every loaded setting and saves the file again when something actually
        /// changed. Repair details go to the log only — they never reach the UI.
        /// </summary>
        private void RepairLoadedSettings(AppSettings settings)
        {
            _settingsChanged = false;

            RepairKeyWords(settings);
            RepairWhisperModel(settings);
            NormalizeTranscriptionLanguage(settings);
            NormalizeUiLanguage(settings);
            NormalizeSelectedTheme(settings);
            RepairHotkey(settings);
            RepairRecordingLimit(settings);

            if (!_settingsChanged)
                return;

            SaveRepairedSettings();
        }

        // Each method below repairs exactly one setting. When a value needs fixing, the
        // method logs it, assigns a safe replacement, and sets _settingsChanged so the
        // file is written again.
        private void RepairKeyWords(AppSettings settings)
        {
            // An explicit keyWords: null entry must not crash the editor or later replacement.
            settings.KeyWords ??= [];
        }

        private void RepairWhisperModel(AppSettings settings)
        {
            if (settings.WhisperModelId == null)
                return;

            if (WhisperModelCatalog.TryGetById(settings.WhisperModelId) != null)
                return;

            Logger.Warning("Invalid Whisper model identifier {ModelId}. Model selection will be disabled.",
                settings.WhisperModelId);
            settings.WhisperModelId = null;
            _settingsChanged = true;
        }

        private void NormalizeTranscriptionLanguage(AppSettings settings)
        {
            var normalizedLanguage = TranscriptionLanguages.Normalize(settings.TranscribeLanguage);
            if (string.Equals(settings.TranscribeLanguage, normalizedLanguage, StringComparison.Ordinal))
                return;

            Logger.Warning("Invalid transcription language code {LanguageCode}. {NormalizedLanguageCode} will be used.",
                settings.TranscribeLanguage,
                normalizedLanguage);
            settings.TranscribeLanguage = normalizedLanguage;
            _settingsChanged = true;
        }

        private void NormalizeUiLanguage(AppSettings settings)
        {
            var normalizedUiLanguage = UiLanguages.Normalize(settings.UiLanguage);
            if (string.Equals(settings.UiLanguage, normalizedUiLanguage, StringComparison.Ordinal))
                return;

            Logger.Warning("Invalid UI language code {LanguageCode}. {NormalizedLanguageCode} will be used.",
                settings.UiLanguage,
                normalizedUiLanguage);
            settings.UiLanguage = normalizedUiLanguage;
            _settingsChanged = true;
        }

        // The theme is a plain string, so an unknown value repairs only this field
        // instead of resetting the whole file like a broken enum would.
        private void NormalizeSelectedTheme(AppSettings settings)
        {
            var normalizedTheme = AppThemes.Normalize(settings.SelectedTheme);
            if (string.Equals(settings.SelectedTheme, normalizedTheme, StringComparison.Ordinal))
                return;

            Logger.Warning("Invalid selected theme {SelectedTheme}. {NormalizedTheme} will be used.",
                settings.SelectedTheme,
                normalizedTheme);
            settings.SelectedTheme = normalizedTheme;
            _settingsChanged = true;
        }

        private void RepairHotkey(AppSettings settings)
        {
            if (settings.Hotkey == null || settings.Hotkey.IsValid(out _))
                return;

            Logger.Warning("Invalid global hotkey. The default hotkey will be restored.");
            settings.Hotkey = HotkeyGesture.CreateDefault();
            _settingsChanged = true;
        }

        private void RepairRecordingLimit(AppSettings settings)
        {
            if (settings.RecordingLimitSeconds >= AppSettings.MinimumRecordingLimitSeconds &&
                settings.RecordingLimitSeconds <= AppSettings.MaximumRecordingLimitSeconds)
                return;

            Logger.Warning(
                "Invalid recording limit {RecordingLimitSeconds}. {DefaultRecordingLimitSeconds} seconds will be used.",
                settings.RecordingLimitSeconds,
                AppSettings.DefaultRecordingLimitSeconds);
            settings.RecordingLimitSeconds = AppSettings.DefaultRecordingLimitSeconds;
            _settingsChanged = true;
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
        /// Returns default settings and writes them to disk so the user has a valid
        /// starting point for manual editing. A write error is only logged — the load
        /// must not fail on it, because memory already holds valid settings.
        /// </summary>
        private AppSettings RestoreDefaults()
        {
            var defaults = new AppSettings();

            _settings = defaults;
            SaveRepairedSettings();
            return defaults;
        }

        /// <summary>Saves the settings after a repair or restore; a write error is only logged.</summary>
        private void SaveRepairedSettings()
        {
            try
            {
                Logger.Information("Saving repaired settings.");
                Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Error(ex, "Failed to save repaired settings.");
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
