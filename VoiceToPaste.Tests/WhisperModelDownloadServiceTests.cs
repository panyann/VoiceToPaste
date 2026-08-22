using System.Net;
using System.Security.Cryptography;
using VoiceToPaste.Models;
using VoiceToPaste.Services;

namespace VoiceToPaste.Tests
{
    /// <summary>Testy pobierania używają małych odpowiedzi HTTP zamiast prawdziwych modeli.</summary>
    public sealed class WhisperModelDownloadServiceTests : IDisposable
    {
        private readonly string _tempDirectory;

        public WhisperModelDownloadServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "VoiceToPaste.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        [Fact]
        public async Task DownloadAsync_ValidFile_FinalizesModelAndReportsProgress()
        {
            var bytes = "model"u8.ToArray();
            var model = CreateModel(bytes);
            using var client = CreateClient(bytes);
            var service = new WhisperModelDownloadService(_tempDirectory, client);
            var progressMessages = new List<WhisperModelDownloadProgress>();

            await service.DownloadAsync(model, new Progress<WhisperModelDownloadProgress>(progressMessages.Add));

            var destinationPath = service.GetModelPath(model);
            Assert.Equal(Path.Combine(_tempDirectory, "LLM"), service.ModelDirectory);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(destinationPath));
            Assert.True(await service.IsModelValidAsync(model));
            Assert.Contains(progressMessages, progress => progress.DownloadedBytes == bytes.Length);
            Assert.Empty(Directory.GetFiles(service.ModelDirectory, "*.part"));
        }

        [Fact]
        public async Task DownloadAsync_InvalidHash_PreservesExistingModelAndDeletesTemporaryFile()
        {
            var expectedBytes = "right"u8.ToArray();
            var existingBytes = "valid"u8.ToArray();
            var downloadedBytes = "wrong"u8.ToArray();
            var model = CreateModel(expectedBytes);
            using var client = CreateClient(downloadedBytes);
            var service = new WhisperModelDownloadService(_tempDirectory, client);
            Directory.CreateDirectory(service.ModelDirectory);
            await File.WriteAllBytesAsync(service.GetModelPath(model), existingBytes);

            await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(model));

            Assert.Equal(existingBytes, await File.ReadAllBytesAsync(service.GetModelPath(model)));
            Assert.Empty(Directory.GetFiles(service.ModelDirectory, "*.part"));
        }

        [Fact]
        public async Task DownloadAsync_UnexpectedContentLength_DoesNotCreateModel()
        {
            var bytes = "model"u8.ToArray();
            var model = CreateModel(bytes);
            using var client = new HttpClient(new StaticResponseHandler(() =>
            {
                var content = new ByteArrayContent(bytes);
                content.Headers.ContentLength = bytes.Length + 1;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            }));
            var service = new WhisperModelDownloadService(_tempDirectory, client);

            await Assert.ThrowsAsync<InvalidDataException>(() => service.DownloadAsync(model));

            Assert.False(File.Exists(service.GetModelPath(model)));
        }

        [Fact]
        public async Task DownloadAsync_CancelledBeforeStart_DoesNotCreateModel()
        {
            var bytes = "model"u8.ToArray();
            var model = CreateModel(bytes);
            using var client = CreateClient(bytes);
            var service = new WhisperModelDownloadService(_tempDirectory, client);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.DownloadAsync(model, cancellationToken: cancellation.Token));

            Assert.False(File.Exists(service.GetModelPath(model)));
        }

        [Fact]
        public async Task DownloadAsync_ExistingValidModel_DoesNotRequestDownload()
        {
            var bytes = "model"u8.ToArray();
            var model = CreateModel(bytes);
            using var client = new HttpClient(new StaticResponseHandler(() => throw new InvalidOperationException("HTTP nie powinno zostać wywołane.")));
            var service = new WhisperModelDownloadService(_tempDirectory, client);
            Directory.CreateDirectory(service.ModelDirectory);
            await File.WriteAllBytesAsync(service.GetModelPath(model), bytes);

            await service.DownloadAsync(model);

            Assert.Equal(bytes, await File.ReadAllBytesAsync(service.GetModelPath(model)));
        }

        [Fact]
        public async Task IsModelValidAsync_SameSizeFileWithDifferentHash_ReturnsFalse()
        {
            var validBytes = "right"u8.ToArray();
            var corruptedBytes = "wrong"u8.ToArray();
            var model = CreateModel(validBytes);
            using var client = CreateClient(validBytes);
            var service = new WhisperModelDownloadService(_tempDirectory, client);
            Directory.CreateDirectory(service.ModelDirectory);
            await File.WriteAllBytesAsync(service.GetModelPath(model), corruptedBytes);

            var isValid = await service.IsModelValidAsync(model);

            Assert.False(isValid);
        }

        private static WhisperModel CreateModel(byte[] bytes, string? sha256 = null) =>
            new(
                "test",
                "Test",
                "WhisperModelTinyDescription",
                "test.bin",
                bytes.Length,
                new Uri("https://example.test/test.bin"),
                sha256 ?? Convert.ToHexString(SHA256.HashData(bytes)));

        private static HttpClient CreateClient(byte[] bytes) =>
            new(new StaticResponseHandler(() =>
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));

        private sealed class StaticResponseHandler(Func<HttpResponseMessage> responseFactory) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(responseFactory());
        }
    }
}
