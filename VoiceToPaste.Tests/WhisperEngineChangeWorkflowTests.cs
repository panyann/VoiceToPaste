using VoiceToPaste.Models;
using VoiceToPaste.Services;

namespace VoiceToPaste.Tests
{
    /// <summary>
    /// Testy workflow zmiany silnika Whisper na prawdziwym SettingsService z własnym
    /// katalogiem tymczasowym — bez delegatów i liczników wywołań.
    /// </summary>
    public sealed class WhisperEngineChangeWorkflowTests : IDisposable
    {
        private readonly string _tempDirectory;

        public WhisperEngineChangeWorkflowTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "VoiceToPaste.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        private WhisperEngineChangeWorkflow CreateWorkflow(AppSettings settings) =>
            new(settings, new SettingsService(_tempDirectory));

        [Fact]
        public void EvaluateChange_SameEngine_ReturnsNoChange()
        {
            var settings = new AppSettings { TranscriptionEngine = TranscriptionBackend.Auto };
            var workflow = CreateWorkflow(settings);

            var step = workflow.EvaluateChange(TranscriptionBackend.Auto, cudaInstalled: false);

            Assert.Equal(WhisperEngineChangeStep.NoChange, step);
        }

        [Fact]
        public void EvaluateChange_GpuWithoutCuda_ReturnsRequiresCudaInstall()
        {
            var settings = new AppSettings { TranscriptionEngine = TranscriptionBackend.Cpu };
            var workflow = CreateWorkflow(settings);

            var step = workflow.EvaluateChange(TranscriptionBackend.Gpu, cudaInstalled: false);

            Assert.Equal(WhisperEngineChangeStep.RequiresCudaInstall, step);
        }

        [Fact]
        public void EvaluateChange_GpuWithCuda_ReturnsReadyToSave()
        {
            var settings = new AppSettings { TranscriptionEngine = TranscriptionBackend.Cpu };
            var workflow = CreateWorkflow(settings);

            var step = workflow.EvaluateChange(TranscriptionBackend.Gpu, cudaInstalled: true);

            Assert.Equal(WhisperEngineChangeStep.ReadyToSave, step);
        }

        [Theory]
        [InlineData(TranscriptionBackend.Auto)]
        [InlineData(TranscriptionBackend.Cpu)]
        public void EvaluateChange_NonGpuEngine_IgnoresCudaState(TranscriptionBackend selectedEngine)
        {
            var settings = new AppSettings { TranscriptionEngine = TranscriptionBackend.Gpu };
            var workflow = CreateWorkflow(settings);

            var step = workflow.EvaluateChange(selectedEngine, cudaInstalled: false);

            Assert.Equal(WhisperEngineChangeStep.ReadyToSave, step);
        }

        [Fact]
        public void EvaluateChange_DoesNotMutateSettingsOrCreateFile()
        {
            var settings = new AppSettings { TranscriptionEngine = TranscriptionBackend.Cpu };
            var workflow = CreateWorkflow(settings);

            workflow.EvaluateChange(TranscriptionBackend.Gpu, cudaInstalled: false);

            Assert.Equal(TranscriptionBackend.Cpu, settings.TranscriptionEngine);
            Assert.False(File.Exists(Path.Combine(_tempDirectory, "settings.yaml")));
        }

        [Fact]
        public void SaveChange_PersistsEngineToSettingsFile()
        {
            var settings = new AppSettings { TranscriptionEngine = TranscriptionBackend.Cpu };
            var workflow = CreateWorkflow(settings);

            workflow.SaveChange(TranscriptionBackend.Gpu);

            Assert.Equal(TranscriptionBackend.Gpu, settings.TranscriptionEngine);
            // Świeża instancja serwisu — udowadniamy, że silnik siedzi w pliku, nie w pamięci.
            var reloaded = new SettingsService(_tempDirectory).Load();
            Assert.Equal(TranscriptionBackend.Gpu, reloaded.TranscriptionEngine);
        }

        [Fact]
        public void SaveChange_AfterCudaInstall_CompletesTheFormFlow()
        {
            var settings = new AppSettings { TranscriptionEngine = TranscriptionBackend.Cpu };
            var workflow = CreateWorkflow(settings);

            var step = workflow.EvaluateChange(TranscriptionBackend.Gpu, cudaInstalled: false);
            Assert.Equal(WhisperEngineChangeStep.RequiresCudaInstall, step);

            // Formularz instaluje CUDA i dopiero wtedy zapisuje zmianę silnika.
            workflow.SaveChange(TranscriptionBackend.Gpu);

            Assert.Equal(TranscriptionBackend.Gpu, settings.TranscriptionEngine);
        }

        [Fact]
        public void SaveChange_SaveFails_RollsBackEngineAndRethrows()
        {
            var settings = new AppSettings { TranscriptionEngine = TranscriptionBackend.Cpu };
            var service = new SettingsService(_tempDirectory);
            // Katalog w miejscu pliku tymczasowego deterministycznie wywala zapis atomowy.
            Directory.CreateDirectory(service.SettingsPath + ".tmp");
            var workflow = new WhisperEngineChangeWorkflow(settings, service);

            Assert.Throws<UnauthorizedAccessException>(() => workflow.SaveChange(TranscriptionBackend.Gpu));

            Assert.Equal(TranscriptionBackend.Cpu, settings.TranscriptionEngine);
        }
    }
}
