using VoiceToPaste.Services;
using VoiceToPaste.Models;

namespace VoiceToPaste.Tests
{
    /// <summary>
    /// Testy stanu singletona transkrypcji. Używają nieistniejącej ścieżki modelu —
    /// inicjalizacja szybko pada, a prawdziwe ~3 GB nigdy nie jest ładowane.
    /// </summary>
    public sealed class TranscriptionServiceTests
    {
        private static TranscriptionService CreateServiceWithMissingModel() =>
            new(Path.Combine(Path.GetTempPath(), "VoiceToPaste.Tests", "no-such-model.bin"));

        private static WhisperModel SelectedModel => WhisperModelCatalog.GetById(WhisperModelCatalog.DefaultModelId);

        [Fact]
        public async Task TranscribeAsync_BeforeInitialization_ThrowsClearError()
        {
            using var service = CreateServiceWithMissingModel();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.TranscribeAsync(new float[16000]));

            Assert.Contains(nameof(TranscriptionService.StartInitializationAsync), ex.Message);
        }

        [Fact]
        public async Task StartInitializationAsync_TwiceWithSameBackend_LoadsOnlyOnce()
        {
            using var service = CreateServiceWithMissingModel();

            var first = service.StartInitializationAsync(TranscriptionBackend.Auto, SelectedModel);
            var second = service.StartInitializationAsync(TranscriptionBackend.Auto, SelectedModel);

            // To samo zadanie — drugie ładowanie nigdy nie rusza.
            Assert.Same(first, second);
            Assert.Equal(TranscriptionServiceState.Loading, service.State);
            Assert.Equal(TranscriptionBackend.Auto, service.SelectedBackend);

            await Assert.ThrowsAnyAsync<Exception>(() => first);
            Assert.Equal(TranscriptionServiceState.Failed, service.State);
            Assert.NotNull(service.InitializationError);
        }

        [Fact]
        public void StartInitializationAsync_DifferentBackendAfterStart_RequiresRestart()
        {
            using var service = CreateServiceWithMissingModel();
            _ = service.StartInitializationAsync(TranscriptionBackend.Auto, SelectedModel);

            // Lambda blokowa wymusza overload dla kodu synchronicznego —
            // wyjątek leci przed zwróceniem zadania.
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                _ = service.StartInitializationAsync(TranscriptionBackend.Cpu, SelectedModel);
            });

            Assert.Contains("restart", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task FailedInitialization_ExposesStateAndError()
        {
            using var service = CreateServiceWithMissingModel();

            await Assert.ThrowsAnyAsync<Exception>(
                () => service.StartInitializationAsync(TranscriptionBackend.Cpu, SelectedModel));

            Assert.Equal(TranscriptionServiceState.Failed, service.State);
            Assert.NotNull(service.InitializationError);
            Assert.Equal(TranscriptionBackend.Cpu, service.SelectedBackend);
            Assert.False(service.State == TranscriptionServiceState.Ready);
        }

        [Fact]
        public async Task Dispose_ThenCallsThrowObjectDisposedException()
        {
            var service = CreateServiceWithMissingModel();
            service.Dispose();

            Assert.Equal(TranscriptionServiceState.Disposed, service.State);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                _ = service.StartInitializationAsync(TranscriptionBackend.Auto, SelectedModel);
            });
            await Assert.ThrowsAsync<ObjectDisposedException>(() => service.TranscribeAsync(new float[16000]));
        }

        [Fact]
        public void Dispose_Twice_DoesNotThrow()
        {
            var service = CreateServiceWithMissingModel();

            service.Dispose();
            service.Dispose();

            Assert.Equal(TranscriptionServiceState.Disposed, service.State);
        }

        [Fact]
        public void SetLanguage_BeforeInitialization_NormalizesAndRemembersLanguage()
        {
            using var service = CreateServiceWithMissingModel();

            service.SetLanguage("en-US");

            Assert.Equal("en", service.Language);
            Assert.Equal(TranscriptionServiceState.NotStarted, service.State);
        }

        [Fact]
        public async Task SetLanguage_DuringInitialization_UpdatesSharedLanguage()
        {
            using var service = CreateServiceWithMissingModel();
            var initialization = service.StartInitializationAsync(TranscriptionBackend.Cpu, SelectedModel);

            service.SetLanguage(TranscriptionLanguages.AutoDetectCode);

            Assert.Equal(TranscriptionLanguages.AutoDetectCode, service.Language);
            await Assert.ThrowsAnyAsync<Exception>(() => initialization);
        }

        [Fact]
        public void SetLanguage_AfterDispose_ThrowsObjectDisposedException()
        {
            var service = CreateServiceWithMissingModel();
            service.Dispose();

            Assert.Throws<ObjectDisposedException>(() => service.SetLanguage("en"));
        }

        [Fact]
        public void NewService_StartsAsNotStarted()
        {
            using var service = CreateServiceWithMissingModel();

            Assert.Equal(TranscriptionServiceState.NotStarted, service.State);
            Assert.Null(service.InitializationError);
            Assert.Null(service.LoadedRuntime);
            Assert.Null(service.SelectedBackend);
            Assert.Equal(TranscriptionLanguages.GetSystemDefaultCode(), service.Language);
        }
    }
}
