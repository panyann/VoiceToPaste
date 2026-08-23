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

        private SettingsService CreateLoadedService(TranscriptionBackend initialEngine)
        {
            var service = new SettingsService(_tempDirectory);
            service.Load();
            service.Settings.TranscriptionEngine = initialEngine;
            return service;
        }

        private WhisperEngineChangeWorkflow CreateWorkflow(TranscriptionBackend initialEngine) =>
            new(CreateLoadedService(initialEngine));

        [Fact]
        public void EvaluateChange_SameEngine_ReturnsNoChange()
        {
            var workflow = CreateWorkflow(TranscriptionBackend.Auto);

            var step = workflow.EvaluateChange(TranscriptionBackend.Auto, cudaInstalled: false);

            Assert.Equal(WhisperEngineChangeStep.NoChange, step);
        }

        [Fact]
        public void EvaluateChange_GpuWithoutCuda_ReturnsRequiresCudaInstall()
        {
            var workflow = CreateWorkflow(TranscriptionBackend.Cpu);

            var step = workflow.EvaluateChange(TranscriptionBackend.Gpu, cudaInstalled: false);

            Assert.Equal(WhisperEngineChangeStep.RequiresCudaInstall, step);
        }

        [Fact]
        public void EvaluateChange_GpuWithCuda_ReturnsReadyToSave()
        {
            var workflow = CreateWorkflow(TranscriptionBackend.Cpu);

            var step = workflow.EvaluateChange(TranscriptionBackend.Gpu, cudaInstalled: true);

            Assert.Equal(WhisperEngineChangeStep.ReadyToSave, step);
        }

        [Theory]
        [InlineData(TranscriptionBackend.Auto)]
        [InlineData(TranscriptionBackend.Cpu)]
        public void EvaluateChange_NonGpuEngine_IgnoresCudaState(TranscriptionBackend selectedEngine)
        {
            var workflow = CreateWorkflow(TranscriptionBackend.Gpu);

            var step = workflow.EvaluateChange(selectedEngine, cudaInstalled: false);

            Assert.Equal(WhisperEngineChangeStep.ReadyToSave, step);
        }

        [Fact]
        public void EvaluateChange_DoesNotMutateSettingsOrWriteFile()
        {
            var service = CreateLoadedService(TranscriptionBackend.Cpu);
            var workflow = new WhisperEngineChangeWorkflow(service);
            var yamlBefore = File.ReadAllText(service.SettingsPath);

            workflow.EvaluateChange(TranscriptionBackend.Gpu, cudaInstalled: false);

            Assert.Equal(TranscriptionBackend.Cpu, service.Settings.TranscriptionEngine);
            Assert.Equal(yamlBefore, File.ReadAllText(service.SettingsPath));
        }

        [Fact]
        public void SaveChange_PersistsEngineToSettingsFile()
        {
            var service = CreateLoadedService(TranscriptionBackend.Cpu);
            var workflow = new WhisperEngineChangeWorkflow(service);

            workflow.SaveChange(TranscriptionBackend.Gpu);

            Assert.Equal(TranscriptionBackend.Gpu, service.Settings.TranscriptionEngine);
            // Świeża instancja serwisu — udowadniamy, że silnik siedzi w pliku, nie w pamięci.
            var reloaded = new SettingsService(_tempDirectory).Load();
            Assert.Equal(TranscriptionBackend.Gpu, reloaded.TranscriptionEngine);
        }

        [Fact]
        public void SaveChange_AfterCudaInstall_CompletesTheFormFlow()
        {
            var service = CreateLoadedService(TranscriptionBackend.Cpu);
            var workflow = new WhisperEngineChangeWorkflow(service);

            var step = workflow.EvaluateChange(TranscriptionBackend.Gpu, cudaInstalled: false);
            Assert.Equal(WhisperEngineChangeStep.RequiresCudaInstall, step);

            // Formularz instaluje CUDA i dopiero wtedy zapisuje zmianę silnika.
            workflow.SaveChange(TranscriptionBackend.Gpu);

            Assert.Equal(TranscriptionBackend.Gpu, service.Settings.TranscriptionEngine);
        }

        [Fact]
        public void SaveChange_SaveFails_RollsBackEngineAndRethrows()
        {
            var service = new SettingsService(_tempDirectory);
            // Katalog w miejscu pliku tymczasowego deterministycznie wywala zapis atomowy.
            Directory.CreateDirectory(service.SettingsPath + ".tmp");
            service.Load();
            service.Settings.TranscriptionEngine = TranscriptionBackend.Cpu;
            var workflow = new WhisperEngineChangeWorkflow(service);

            var exception = Assert.ThrowsAny<Exception>(() => workflow.SaveChange(TranscriptionBackend.Gpu));

            Assert.True(exception is UnauthorizedAccessException or IOException);

            Assert.Equal(TranscriptionBackend.Cpu, service.Settings.TranscriptionEngine);
        }
    }
}
