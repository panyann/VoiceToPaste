using VoiceToPaste.Models;
using VoiceToPaste.Services;

namespace VoiceToPaste.Tests
{
    /// <summary>
    /// Testy odczytu i zapisu settings.yaml. Każdy test pracuje na własnym katalogu
    /// tymczasowym, żeby nie dotykać plików obok aplikacji.
    /// </summary>
    public sealed class SettingsServiceTests : IDisposable
    {
        private readonly string _tempDirectory;

        public SettingsServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "VoiceToPaste.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        private SettingsService CreateService() => new(_tempDirectory);

        [Fact]
        public void Load_MissingFile_ReturnsDefaultsAndCreatesFile()
        {
            var service = CreateService();

            var settings = service.Load();

            Assert.Equal(TranscriptionBackend.Auto, settings.TranscriptionEngine);
            Assert.Null(settings.WhisperModelId);
            Assert.Equal(TranscriptionLanguages.GetSystemDefaultCode(), settings.TranscribeLanguage);
            Assert.Equal(UiLanguages.GetSystemDefaultCode(), settings.UiLanguage);
            Assert.Equal(AppThemes.Dark, settings.SelectedTheme);
            Assert.False(settings.StartInTray);
            Assert.False(settings.AutoStart);
            Assert.Equal(AppSettings.DefaultRecordingLimitSeconds, settings.RecordingLimitSeconds);
            Assert.Equal("Ctrl + Shift + Space", settings.Hotkey?.ToDisplayString());
            Assert.Empty(settings.KeyWords);
            Assert.True(File.Exists(service.SettingsPath));
            Assert.Contains("autoStart: false", File.ReadAllText(service.SettingsPath));
            Assert.Contains("recordingLimitSeconds: 60", File.ReadAllText(service.SettingsPath));
            Assert.Contains($"uiLanguage: {settings.UiLanguage}", File.ReadAllText(service.SettingsPath));
            Assert.Contains("selectedTheme: dark", File.ReadAllText(service.SettingsPath));
        }

        [Theory]
        [InlineData("auto", TranscriptionBackend.Auto)]
        [InlineData("gpu", TranscriptionBackend.Gpu)]
        [InlineData("cpu", TranscriptionBackend.Cpu)]
        public void Load_ValidEngineValues_AreReadCorrectly(string engine, TranscriptionBackend expected)
        {
            var service = CreateService();
            var yaml = $"transcriptionEngine: {engine}{Environment.NewLine}";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Equal(expected, settings.TranscriptionEngine);
            // A clean load must not rewrite the file.
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_ValidWhisperModelId_IsReadCorrectly()
        {
            var service = CreateService();
            var yaml = "whisperModelId: small\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Equal("small", settings.WhisperModelId);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_InvalidWhisperModelId_DisablesModelAndRepairsFile()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "whisperModelId: removed-model\n");

            var settings = service.Load();

            Assert.Null(settings.WhisperModelId);
            var repairedYaml = File.ReadAllText(service.SettingsPath);
            Assert.Contains("whisperModelId:", repairedYaml);
            Assert.DoesNotContain("removed-model", repairedYaml);
            Assert.Null(CreateService().Load().WhisperModelId);
        }

        [Theory]
        [InlineData("pl", "pl")]
        [InlineData("en", "en")]
        [InlineData("auto", "auto")]
        public void Load_ValidTranscribeLanguageValues_AreReadCorrectly(string language, string expected)
        {
            var service = CreateService();
            var yaml = $"transcribeLanguage: {language}{Environment.NewLine}";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Equal(expected, settings.TranscribeLanguage);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Theory]
        [InlineData("pl", "pl")]
        [InlineData("en", "en")]
        [InlineData("PL", "pl")]
        public void Load_ValidUiLanguageValues_AreReadCorrectly(string language, string expected)
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, $"uiLanguage: {language}{Environment.NewLine}");

            var settings = service.Load();

            Assert.Equal(expected, settings.UiLanguage);
        }

        [Fact]
        public void Load_UnknownUiLanguage_RestoresSystemLanguageAndRepairsFile()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "uiLanguage: xyz\n");

            var settings = service.Load();

            Assert.Equal(UiLanguages.GetSystemDefaultCode(), settings.UiLanguage);
            Assert.Contains($"uiLanguage: {settings.UiLanguage}", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_UiLanguageDoesNotChangeTranscribeLanguage()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "uiLanguage: en\ntranscribeLanguage: pl\n");

            var settings = service.Load();

            Assert.Equal("en", settings.UiLanguage);
            Assert.Equal("pl", settings.TranscribeLanguage);
        }

        [Fact]
        public void Load_MissingSelectedTheme_ReturnsDarkTheme()
        {
            var service = CreateService();
            var yaml = "transcriptionEngine: cpu\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Equal(AppThemes.Dark, settings.SelectedTheme);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Theory]
        [InlineData("dark")]
        [InlineData("light")]
        public void Load_ValidSelectedThemeValues_AreReadCorrectly(string selectedTheme)
        {
            var service = CreateService();
            var yaml = $"selectedTheme: {selectedTheme}\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Equal(selectedTheme, settings.SelectedTheme);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Theory]
        [InlineData("blue", "dark")]
        [InlineData("DARK", "dark")]
        [InlineData("Light", "light")]
        public void Load_NonCanonicalSelectedTheme_IsNormalizedAndRepairsFile(string selectedTheme, string expected)
        {
            var service = CreateService();
            File.WriteAllText(
                service.SettingsPath,
                $"transcriptionEngine: cpu\nselectedTheme: {selectedTheme}\n");

            var settings = service.Load();

            // The repair touches only the theme — the remaining settings stay unchanged.
            Assert.Equal(TranscriptionBackend.Cpu, settings.TranscriptionEngine);
            Assert.Equal(expected, settings.SelectedTheme);
            var repairedYaml = File.ReadAllText(service.SettingsPath);
            Assert.Contains($"selectedTheme: {expected}", repairedYaml);
            Assert.Contains("transcriptionEngine: cpu", repairedYaml);
        }

        [Fact]
        public void Load_SelectedThemeSetToNull_RestoresDarkAndRepairsFile()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "selectedTheme: null\n");

            var settings = service.Load();

            Assert.Equal(AppThemes.Dark, settings.SelectedTheme);
            Assert.Contains("selectedTheme: dark", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_MissingStartInTray_ReturnsFalse()
        {
            var service = CreateService();
            var yaml = "transcriptionEngine: cpu\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.False(settings.StartInTray);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_MissingAutoStart_ReturnsFalse()
        {
            var service = CreateService();
            var yaml = "transcriptionEngine: cpu\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.False(settings.AutoStart);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_MissingRecordingLimit_ReturnsDefaultValue()
        {
            var service = CreateService();
            var yaml = "transcriptionEngine: cpu\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Equal(AppSettings.DefaultRecordingLimitSeconds, settings.RecordingLimitSeconds);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(3601)]
        public void Load_RecordingLimitOutsideAllowedRange_RestoresDefaultAndRepairsFile(int recordingLimitSeconds)
        {
            var service = CreateService();
            File.WriteAllText(
                service.SettingsPath,
                $"transcriptionEngine: cpu\nrecordingLimitSeconds: {recordingLimitSeconds}\n");

            var settings = service.Load();

            Assert.Equal(TranscriptionBackend.Cpu, settings.TranscriptionEngine);
            Assert.Equal(AppSettings.DefaultRecordingLimitSeconds, settings.RecordingLimitSeconds);
            Assert.Contains("recordingLimitSeconds: 60", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_HotkeySetToNull_DisablesGlobalHotkey()
        {
            var service = CreateService();
            var yaml = "hotkey: null\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Null(settings.Hotkey);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_MissingHotkey_ReturnsDefaultHotkey()
        {
            var service = CreateService();
            var yaml = "transcriptionEngine: cpu\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Equal("Ctrl + Shift + Space", settings.Hotkey?.ToDisplayString());
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_InvalidHotkey_RestoresDefaultAndRepairsFile()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "hotkey:\n  key: Escape\n  modifiers: []\n");

            var settings = service.Load();

            Assert.Equal("Ctrl + Shift + Space", settings.Hotkey?.ToDisplayString());
            var repairedYaml = File.ReadAllText(service.SettingsPath);
            Assert.Contains("key: Space", repairedYaml);
            Assert.Contains("- control", repairedYaml);
            Assert.Contains("- shift", repairedYaml);
        }

        [Fact]
        public void Load_ExistingCtrlSpaceHotkey_PreservesUserSetting()
        {
            var service = CreateService();
            var yaml = "hotkey:\n  key: Space\n  modifiers:\n  - control\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Equal("Ctrl + Space", settings.Hotkey?.ToDisplayString());
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_CorruptedYaml_ReturnsDefaultsAndRepairsFile()
        {
            var service = CreateService();
            // Niezamknięty cudzysłów — YamlDotNet na pewno zgłosi błąd parsowania.
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: \"unterminated");

            var settings = service.Load();

            Assert.Equal(TranscriptionBackend.Auto, settings.TranscriptionEngine);
            Assert.Contains("transcriptionEngine: auto", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_UnknownEngineValue_ReturnsDefaultsAndRepairsFile()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: tpu");

            var settings = service.Load();

            Assert.Equal(TranscriptionBackend.Auto, settings.TranscriptionEngine);
            Assert.Contains("transcriptionEngine: auto", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_UnknownTranscribeLanguage_RestoresSystemLanguageAndRepairsFile()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: cpu\ntranscribeLanguage: xyz\n");

            var settings = service.Load();

            Assert.Equal(TranscriptionBackend.Cpu, settings.TranscriptionEngine);
            Assert.Equal(TranscriptionLanguages.GetSystemDefaultCode(), settings.TranscribeLanguage);
            Assert.Contains($"transcribeLanguage: {settings.TranscribeLanguage}", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsSettings()
        {
            var service = CreateService();
            service.Load();
            var settings = service.Settings;
            settings.TranscriptionEngine = TranscriptionBackend.Gpu;
            settings.WhisperModelId = "medium";
            settings.TranscribeLanguage = "en";
            settings.UiLanguage = "pl";
            settings.SelectedTheme = AppThemes.Light;
            settings.StartInTray = true;
            settings.AutoStart = true;
            settings.RecordingLimitSeconds = 120;
            settings.Hotkey = new HotkeyGesture
            {
                Key = "F12",
                Modifiers = [HotkeyModifier.Control, HotkeyModifier.Shift],
            };
            service.Save();

            // Świeża instancja serwisu — udowadniamy, że dane siedzą w pliku, nie w pamięci.
            var reloaded = CreateService().Load();

            Assert.Equal(TranscriptionBackend.Gpu, reloaded.TranscriptionEngine);
            Assert.Equal("medium", reloaded.WhisperModelId);
            Assert.Equal("en", reloaded.TranscribeLanguage);
            Assert.Equal("pl", reloaded.UiLanguage);
            Assert.Equal(AppThemes.Light, reloaded.SelectedTheme);
            Assert.True(reloaded.StartInTray);
            Assert.True(reloaded.AutoStart);
            Assert.Equal(120, reloaded.RecordingLimitSeconds);
            Assert.Equal("Ctrl + Shift + F12", reloaded.Hotkey?.ToDisplayString());
            Assert.Contains("transcriptionEngine: gpu", File.ReadAllText(service.SettingsPath));
            Assert.Contains("whisperModelId: medium", File.ReadAllText(service.SettingsPath));
            Assert.Contains("transcribeLanguage: en", File.ReadAllText(service.SettingsPath));
            Assert.Contains("uiLanguage: pl", File.ReadAllText(service.SettingsPath));
            Assert.Contains("selectedTheme: light", File.ReadAllText(service.SettingsPath));
            Assert.Contains("startInTray: true", File.ReadAllText(service.SettingsPath));
            Assert.Contains("autoStart: true", File.ReadAllText(service.SettingsPath));
            Assert.Contains("recordingLimitSeconds: 120", File.ReadAllText(service.SettingsPath));
            Assert.Contains("hotkey:", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_MissingKeyWords_ReturnsEmptyList()
        {
            var service = CreateService();
            var yaml = "transcriptionEngine: cpu\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Empty(settings.KeyWords);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_KeyWordsSetToNull_ReturnsEmptyList()
        {
            var service = CreateService();
            var yaml = "transcriptionEngine: cpu\nkeyWords: null\n";
            File.WriteAllText(service.SettingsPath, yaml);

            var settings = service.Load();

            Assert.Empty(settings.KeyWords);
            Assert.Equal(yaml, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsKeyWords()
        {
            var service = CreateService();
            service.Load();
            service.Settings.KeyWords =
            [
                new DGV_KeyWords { Key = "plik agent", Word = "AGENTS.md" },
                new DGV_KeyWords { Key = "voice to paste", Word = "VoiceToPaste" },
            ];
            service.Save();

            // Świeża instancja serwisu — udowadniamy, że dane siedzą w pliku, nie w pamięci.
            var reloaded = CreateService().Load();

            Assert.Equal(2, reloaded.KeyWords.Count);
            Assert.Equal("plik agent", reloaded.KeyWords[0].Key);
            Assert.Equal("AGENTS.md", reloaded.KeyWords[0].Word);
            Assert.Equal("voice to paste", reloaded.KeyWords[1].Key);
            Assert.Equal("VoiceToPaste", reloaded.KeyWords[1].Word);
            Assert.Contains("keyWords:", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Save_DoesNotLeaveTempFile()
        {
            var service = CreateService();
            service.Load();

            service.Save();

            Assert.False(File.Exists(service.SettingsPath + ".tmp"));
        }

        [Fact]
        public void Settings_BeforeLoad_Throws()
        {
            var service = CreateService();

            Assert.Throws<InvalidOperationException>(() => service.Settings);
        }

        [Fact]
        public void Save_BeforeLoad_Throws()
        {
            var service = CreateService();

            Assert.Throws<InvalidOperationException>(() => service.Save());
        }

        [Fact]
        public void Load_CalledTwice_Throws()
        {
            var service = CreateService();
            service.Load();

            Assert.Throws<InvalidOperationException>(() => service.Load());
        }
    }
}
