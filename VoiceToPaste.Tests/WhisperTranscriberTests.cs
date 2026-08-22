using VoiceToPaste.Models;

namespace VoiceToPaste.Tests
{
    /// <summary>
    /// Testy stanu transkrybera, które nie wymagają pliku modelu —
    /// konstruktor niczego nie ładuje, a błędna ścieżka kończy się szybkim wyjątkiem.
    /// </summary>
    public sealed class WhisperTranscriberTests
    {
        [Fact]
        public async Task CallsAfterDispose_ThrowObjectDisposedException()
        {
            var transcriber = new WhisperTranscriber("model.bin");
            transcriber.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => transcriber.InitializeAsync());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => transcriber.TranscribeAsync(new float[16000]));
        }

        [Fact]
        public void Dispose_Twice_DoesNotThrow()
        {
            var transcriber = new WhisperTranscriber("model.bin");

            transcriber.Dispose();
            transcriber.Dispose();
        }

        [Fact]
        public async Task InitializeAsync_WithMissingModel_RecordsErrorAndFailsFast()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), "VoiceToPaste.Tests", "no-such-model.bin");
            using var transcriber = new WhisperTranscriber(missingPath);

            var first = await Assert.ThrowsAnyAsync<Exception>(() => transcriber.InitializeAsync());

            Assert.False(transcriber.IsReady);
            Assert.Same(first, transcriber.InitializationError);

            // Fail-fast: drugie wywołanie rzuca tę samą instancję błędu,
            // zamiast ponownie próbować otwierać nieistniejący plik.
            var second = await Assert.ThrowsAnyAsync<Exception>(() => transcriber.InitializeAsync());
            Assert.Same(first, second);
        }

        [Fact]
        public void NewTranscriber_IsNotReadyAndHasNoDiagnostics()
        {
            using var transcriber = new WhisperTranscriber("model.bin", TranscriptionBackend.Cpu);

            Assert.False(transcriber.IsReady);
            Assert.Null(transcriber.LoadedRuntime);
            Assert.Null(transcriber.InitializationError);
            Assert.Equal(TranscriptionBackend.Cpu, transcriber.Backend);
            Assert.Equal(TranscriptionLanguages.GetSystemDefaultCode(), transcriber.Language);
        }

        [Fact]
        public void Language_Change_IsNormalizedAndDoesNotRequireModelLoading()
        {
            using var transcriber = new WhisperTranscriber("model.bin");

            transcriber.Language = "en-US";

            Assert.Equal("en", transcriber.Language);
            Assert.False(transcriber.IsReady);
        }

        [Fact]
        public void Language_CanEnableAutomaticDetection()
        {
            using var transcriber = new WhisperTranscriber("model.bin");

            transcriber.Language = TranscriptionLanguages.AutoDetectCode;

            Assert.Equal(TranscriptionLanguages.AutoDetectCode, transcriber.Language);
        }

        [Fact]
        public void GetModelPath_UsesOnlyLlmDirectoryBesideExecutable()
        {
            var model = WhisperModelCatalog.GetById("small");

            var path = WhisperTranscriber.GetModelPath(model);

            var executablePath = Assert.IsType<string>(Environment.ProcessPath);
            var applicationDirectory = Assert.IsType<string>(Path.GetDirectoryName(executablePath));

            Assert.Equal(Path.Combine(applicationDirectory, "LLM", "ggml-small.bin"), path);
        }
    }
}
