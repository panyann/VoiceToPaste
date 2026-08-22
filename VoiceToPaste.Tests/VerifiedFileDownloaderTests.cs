using System.Net;
using System.Security.Cryptography;
using VoiceToPaste.Services;

namespace VoiceToPaste.Tests
{
    /// <summary>Testy wspólnego pobieracza bez pobierania rzeczywistych danych z sieci.</summary>
    public sealed class VerifiedFileDownloaderTests : IDisposable
    {
        private readonly string _tempDirectory;

        public VerifiedFileDownloaderTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "VoiceToPaste.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        [Fact]
        public async Task DownloadAsync_ValidResponse_WritesFileAndReturnsHash()
        {
            var bytes = "verified"u8.ToArray();
            var path = Path.Combine(_tempDirectory, "model.part");
            using var client = CreateClient(bytes);
            var downloader = new VerifiedFileDownloader(client);
            var request = new VerifiedFileDownloadRequest(
                new Uri("https://example.test/model.bin"),
                bytes.Length,
                SHA256.HashData(bytes));

            var result = await downloader.DownloadAsync(request, path);

            Assert.Equal(bytes.Length, result.DownloadedBytes);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), Convert.ToHexString(result.Sha256));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        }

        [Fact]
        public async Task DownloadAsync_InvalidHash_ThrowsAndDoesNotReportSuccess()
        {
            var bytes = "wrong"u8.ToArray();
            var path = Path.Combine(_tempDirectory, "model.part");
            using var client = CreateClient(bytes);
            var downloader = new VerifiedFileDownloader(client);
            var request = new VerifiedFileDownloadRequest(
                new Uri("https://example.test/model.bin"),
                bytes.Length,
                SHA256.HashData("right"u8));

            await Assert.ThrowsAsync<InvalidDataException>(() => downloader.DownloadAsync(request, path));

            Assert.True(File.Exists(path));
        }

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
