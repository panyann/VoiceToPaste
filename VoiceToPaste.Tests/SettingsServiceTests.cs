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
            Assert.False(settings.StartInTray);
            Assert.False(settings.AutoStart);
            Assert.Equal(AppSettings.DefaultRecordingLimitSeconds, settings.RecordingLimitSeconds);
            Assert.Equal("Ctrl + Space", settings.Hotkey?.ToDisplayString());
            Assert.Empty(settings.KeyWords);
            Assert.True(File.Exists(service.SettingsPath));
            Assert.Contains("autoStart: false", File.ReadAllText(service.SettingsPath));
            Assert.Contains("recordingLimitSeconds: 60", File.ReadAllText(service.SettingsPath));
            Assert.Contains($"uiLanguage: {settings.UiLanguage}", File.ReadAllText(service.SettingsPath));
            Assert.NotNull(service.LastLoadDiagnostic);
        }

        [Theory]
        [InlineData("auto", TranscriptionBackend.Auto)]
        [InlineData("gpu", TranscriptionBackend.Gpu)]
        [InlineData("cpu", TranscriptionBackend.Cpu)]
        public void Load_ValidEngineValues_AreReadCorrectly(string engine, TranscriptionBackend expected)
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, $"transcriptionEngine: {engine}{Environment.NewLine}");

            var settings = service.Load();

            Assert.Equal(expected, settings.TranscriptionEngine);
            Assert.Null(service.LastLoadDiagnostic);
        }

        [Fact]
        public void Load_ValidWhisperModelId_IsReadCorrectly()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "whisperModelId: small\n");

            var settings = service.Load();

            Assert.Equal("small", settings.WhisperModelId);
            Assert.Null(service.LastLoadDiagnostic);
        }

        [Fact]
        public void Load_InvalidWhisperModelId_DisablesModelAndRepairsFile()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "whisperModelId: removed-model\n");

            var settings = service.Load();

            Assert.Null(settings.WhisperModelId);
            Assert.NotNull(service.LastLoadDiagnostic);
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
            File.WriteAllText(service.SettingsPath, $"transcribeLanguage: {language}{Environment.NewLine}");

            var settings = service.Load();

            Assert.Equal(expected, settings.TranscribeLanguage);
            Assert.Null(service.LastLoadDiagnostic);
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
            Assert.NotNull(service.LastLoadDiagnostic);
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
        public void Load_MissingStartInTray_ReturnsFalse()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: cpu\n");

            var settings = service.Load();

            Assert.False(settings.StartInTray);
            Assert.Null(service.LastLoadDiagnostic);
        }

        [Fact]
        public void Load_MissingAutoStart_ReturnsFalse()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: cpu\n");

            var settings = service.Load();

            Assert.False(settings.AutoStart);
            Assert.Null(service.LastLoadDiagnostic);
        }

        [Fact]
        public void Load_MissingRecordingLimit_ReturnsDefaultValue()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: cpu\n");

            var settings = service.Load();

            Assert.Equal(AppSettings.DefaultRecordingLimitSeconds, settings.RecordingLimitSeconds);
            Assert.Null(service.LastLoadDiagnostic);
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
            Assert.NotNull(service.LastLoadDiagnostic);
            Assert.Contains("recordingLimitSeconds: 60", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_HotkeySetToNull_DisablesGlobalHotkey()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "hotkey: null\n");

            var settings = service.Load();

            Assert.Null(settings.Hotkey);
            Assert.Null(service.LastLoadDiagnostic);
        }

        [Fact]
        public void Load_MissingHotkey_ReturnsDefaultHotkey()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: cpu\n");

            var settings = service.Load();

            Assert.Equal("Ctrl + Space", settings.Hotkey?.ToDisplayString());
            Assert.Null(service.LastLoadDiagnostic);
        }

        [Fact]
        public void Load_InvalidHotkey_RestoresDefaultAndRepairsFile()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "hotkey:\n  key: Escape\n  modifiers: []\n");

            var settings = service.Load();

            Assert.Equal("Ctrl + Space", settings.Hotkey?.ToDisplayString());
            Assert.NotNull(service.LastLoadDiagnostic);
            Assert.Contains("key: Space", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_CorruptedYaml_ReturnsDefaultsAndRepairsFile()
        {
            var service = CreateService();
            // Niezamknięty cudzysłów — YamlDotNet na pewno zgłosi błąd parsowania.
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: \"unterminated");

            var settings = service.Load();

            Assert.Equal(TranscriptionBackend.Auto, settings.TranscriptionEngine);
            Assert.NotNull(service.LastLoadDiagnostic);
            Assert.Contains("transcriptionEngine: auto", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_UnknownEngineValue_ReturnsDefaultsAndRepairsFile()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: tpu");

            var settings = service.Load();

            Assert.Equal(TranscriptionBackend.Auto, settings.TranscriptionEngine);
            Assert.NotNull(service.LastLoadDiagnostic);
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
            Assert.NotNull(service.LastLoadDiagnostic);
            Assert.Contains($"transcribeLanguage: {settings.TranscribeLanguage}", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsSettings()
        {
            var service = CreateService();
            service.Save(new AppSettings
            {
                TranscriptionEngine = TranscriptionBackend.Gpu,
                WhisperModelId = "medium",
                TranscribeLanguage = "en",
                UiLanguage = "pl",
                StartInTray = true,
                AutoStart = true,
                RecordingLimitSeconds = 120,
                Hotkey = new HotkeyGesture
                {
                    Key = "F12",
                    Modifiers = [HotkeyModifier.Control, HotkeyModifier.Shift],
                },
            });

            // Świeża instancja serwisu — udowadniamy, że dane siedzą w pliku, nie w pamięci.
            var reloaded = CreateService().Load();

            Assert.Equal(TranscriptionBackend.Gpu, reloaded.TranscriptionEngine);
            Assert.Equal("medium", reloaded.WhisperModelId);
            Assert.Equal("en", reloaded.TranscribeLanguage);
            Assert.Equal("pl", reloaded.UiLanguage);
            Assert.True(reloaded.StartInTray);
            Assert.True(reloaded.AutoStart);
            Assert.Equal(120, reloaded.RecordingLimitSeconds);
            Assert.Equal("Ctrl + Shift + F12", reloaded.Hotkey?.ToDisplayString());
            Assert.Contains("transcriptionEngine: gpu", File.ReadAllText(service.SettingsPath));
            Assert.Contains("whisperModelId: medium", File.ReadAllText(service.SettingsPath));
            Assert.Contains("transcribeLanguage: en", File.ReadAllText(service.SettingsPath));
            Assert.Contains("uiLanguage: pl", File.ReadAllText(service.SettingsPath));
            Assert.Contains("startInTray: true", File.ReadAllText(service.SettingsPath));
            Assert.Contains("autoStart: true", File.ReadAllText(service.SettingsPath));
            Assert.Contains("recordingLimitSeconds: 120", File.ReadAllText(service.SettingsPath));
            Assert.Contains("hotkey:", File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void Load_MissingKeyWords_ReturnsEmptyList()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: cpu\n");

            var settings = service.Load();

            Assert.Empty(settings.KeyWords);
            Assert.Null(service.LastLoadDiagnostic);
        }

        [Fact]
        public void Load_KeyWordsSetToNull_ReturnsEmptyList()
        {
            var service = CreateService();
            File.WriteAllText(service.SettingsPath, "transcriptionEngine: cpu\nkeyWords: null\n");

            var settings = service.Load();

            Assert.Empty(settings.KeyWords);
            Assert.Null(service.LastLoadDiagnostic);
        }

        [Fact]
        public void Save_ThenLoad_RoundTripsKeyWords()
        {
            var service = CreateService();
            service.Save(new AppSettings
            {
                KeyWords =
                [
                    new DGV_KeyWords { Key = "plik agent", Word = "AGENTS.md" },
                    new DGV_KeyWords { Key = "voice to paste", Word = "VoiceToPaste" },
                ],
            });

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

            service.Save(new AppSettings());

            Assert.False(File.Exists(service.SettingsPath + ".tmp"));
        }
    }
}
