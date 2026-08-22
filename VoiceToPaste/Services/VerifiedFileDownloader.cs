using System.Security.Cryptography;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Services
{
    /// <summary>Parametry pliku pobieranego ze sprawdzeniem integralności.</summary>
    internal sealed record VerifiedFileDownloadRequest(
        Uri DownloadUri,
        long ExpectedSizeBytes,
        byte[] ExpectedSha256);

    /// <summary>Postęp pobierania niezależny od rodzaju pobieranego zasobu.</summary>
    internal sealed record VerifiedFileDownloadProgress(long DownloadedBytes, long? TotalBytes);

    /// <summary>Wynik poprawnie pobranego i zweryfikowanego pliku.</summary>
    internal sealed record VerifiedFileDownloadResult(long DownloadedBytes, byte[] Sha256);

    /// <summary>
    /// Wspólna obsługa pobierania plików binarnych. Zapisuje dane do wskazanej ścieżki
    /// tymczasowej i zwraca sukces wyłącznie wtedy, gdy rozmiar oraz SHA-256 są poprawne.
    /// </summary>
    internal sealed class VerifiedFileDownloader
    {
        private static readonly HttpClient SharedHttpClient = new();
        private readonly HttpClient _httpClient;

        public VerifiedFileDownloader()
            : this(SharedHttpClient)
        {
        }

        internal VerifiedFileDownloader(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Strumieniuje niekodowaną odpowiedź HTTP do pliku, równocześnie obliczając SHA-256.
        /// Plik pozostaje tymczasowy po niepowodzeniu, aby właściciel operacji zdecydował,
        /// kiedy go usunąć i kiedy można atomowo przenieść go pod docelową nazwę.
        /// </summary>
        public async Task<VerifiedFileDownloadResult> DownloadAsync(
            VerifiedFileDownloadRequest request,
            string temporaryPath,
            IProgress<VerifiedFileDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            if (request.ExpectedSizeBytes < 0)
                throw LocalizedExceptionFactory.ArgumentOutOfRange("ExpectedFileSizeMustBeNonNegative", nameof(request));
            if (request.ExpectedSha256.Length != 32)
                throw LocalizedExceptionFactory.Argument("Sha256LengthInvalid", nameof(request));

            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, request.DownloadUri);
            // Pobieramy oryginalne bajty pliku, aby suma kontrolna nie dotyczyła wersji zmienionej przez proxy.
            httpRequest.Headers.AcceptEncoding.ParseAdd("identity");
            using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentEncoding.Any(encoding =>
                    !string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase)))
            {
                throw LocalizedExceptionFactory.InvalidData("UnsupportedResponseEncoding");
            }

            if (response.Content.Headers.ContentLength is long contentLength && contentLength != request.ExpectedSizeBytes)
            {
                throw LocalizedExceptionFactory.InvalidData(
                    "UnexpectedServerFileSize",
                    contentLength,
                    request.ExpectedSizeBytes);
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 128,
                useAsync: true);

            var buffer = new byte[1024 * 128];
            long downloadedBytes = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash.AppendData(buffer, 0, read);
                downloadedBytes += read;
                progress?.Report(new VerifiedFileDownloadProgress(downloadedBytes, request.ExpectedSizeBytes));
            }

            await destination.FlushAsync(cancellationToken);
            if (downloadedBytes != request.ExpectedSizeBytes)
            {
                throw LocalizedExceptionFactory.InvalidData(
                    "UnexpectedDownloadedFileSize",
                    downloadedBytes,
                    request.ExpectedSizeBytes);
            }

            var actualHash = hash.GetHashAndReset();
            if (!CryptographicOperations.FixedTimeEquals(actualHash, request.ExpectedSha256))
            {
                throw LocalizedExceptionFactory.InvalidData(
                    "DownloadedFileIntegrityCheckFailed",
                    Convert.ToHexString(actualHash));
            }

            return new VerifiedFileDownloadResult(downloadedBytes, actualHash);
        }

        internal static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }
    }
}
