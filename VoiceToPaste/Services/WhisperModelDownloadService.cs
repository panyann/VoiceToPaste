using System.Security.Cryptography;
using Serilog;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Services
{
    /// <summary>Stan pobierania modelu Whisper przekazywany do formularza.</summary>
    public sealed record WhisperModelDownloadProgress(string Message, long DownloadedBytes = 0, long? TotalBytes = null);

    /// <summary>
    /// Pobiera pojedynczy model Whisper do katalogu LLM obok EXE. Model jest udostępniany
    /// dopiero po sprawdzeniu rozmiaru i SHA-256, więc niekompletny plik nie trafi do Whispera.
    /// </summary>
    public sealed class WhisperModelDownloadService
    {
        private static readonly ILogger Logger = Log.ForContext<WhisperModelDownloadService>();
        private readonly string _modelDirectory;
        private readonly VerifiedFileDownloader _fileDownloader;

        public WhisperModelDownloadService()
            : this(ApplicationPaths.Directory, new VerifiedFileDownloader())
        {
        }

        // Konstruktor wewnętrzny pozwala testom używać małego katalogu i kontrolowanej odpowiedzi HTTP.
        internal WhisperModelDownloadService(string applicationDirectory, HttpClient httpClient)
            : this(applicationDirectory, new VerifiedFileDownloader(httpClient))
        {
        }

        internal WhisperModelDownloadService(string applicationDirectory, VerifiedFileDownloader fileDownloader)
        {
            _modelDirectory = Path.Combine(applicationDirectory, "LLM");
            _fileDownloader = fileDownloader;
        }

        /// <summary>Katalog docelowy modeli, zawsze bezpośrednio obok pliku wykonywalnego.</summary>
        public string ModelDirectory => _modelDirectory;

        public string GetModelPath(WhisperModel model) => Path.Combine(_modelDirectory, model.FileName);

        /// <summary>Sprawdza asynchronicznie rozmiar i SHA-256 lokalnego pliku modelu.</summary>
        public async Task<bool> IsModelValidAsync(WhisperModel model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            try
            {
                return await IsValidFileAsync(GetModelPath(model), model, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warning(ex, "Failed to verify the local Whisper model {ModelId}.", model.Id);
                return false;
            }
        }

        /// <summary>
        /// Pobiera i weryfikuje model. Plik tymczasowy leży w katalogu docelowym, aby końcowe
        /// zastąpienie odbyło się na tym samym woluminie.
        /// </summary>
        public async Task DownloadAsync(
            WhisperModel model,
            IProgress<WhisperModelDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_modelDirectory);

            var destinationPath = GetModelPath(model);
            if (await IsValidFileAsync(destinationPath, model, cancellationToken))
            {
                progress?.Report(new WhisperModelDownloadProgress(UiStrings.Get("WhisperModelAlreadyDownloaded"), model.SizeBytes, model.SizeBytes));
                return;
            }

            var temporaryPath = Path.Combine(_modelDirectory, $"{model.FileName}.{Guid.NewGuid():N}.part");
            try
            {
                Logger.Information("Starting download of Whisper model {ModelId}. Expected size: {ExpectedBytes} B.",
                    model.Id,
                    model.SizeBytes);
                progress?.Report(new WhisperModelDownloadProgress(UiStrings.Format("WhisperModelDownloading", model.DisplayName), 0, model.SizeBytes));

                await _fileDownloader.DownloadAsync(
                    new VerifiedFileDownloadRequest(model.DownloadUri, model.SizeBytes, Convert.FromHexString(model.Sha256)),
                    temporaryPath,
                    new Progress<VerifiedFileDownloadProgress>(download => progress?.Report(
                        new WhisperModelDownloadProgress(UiStrings.Format("WhisperModelDownloading", model.DisplayName), download.DownloadedBytes, download.TotalBytes))),
                    cancellationToken);

                progress?.Report(new WhisperModelDownloadProgress(UiStrings.Format("WhisperModelVerifying", model.DisplayName), model.SizeBytes, model.SizeBytes));
                if (!await IsValidFileAsync(temporaryPath, model, cancellationToken))
                    throw LocalizedExceptionFactory.InvalidData("WhisperModelIntegrityCheckFailed", model.DisplayName);

                // Plik jest poprawny dopiero w tym miejscu; replace na tym samym woluminie nie zostawia uciętego modelu.
                File.Move(temporaryPath, destinationPath, overwrite: true);
                Logger.Information("Downloaded and verified Whisper model {ModelId}.", model.Id);
                progress?.Report(new WhisperModelDownloadProgress(UiStrings.Format("WhisperModelDownloadCompleted", model.DisplayName), model.SizeBytes, model.SizeBytes));
            }
            catch (OperationCanceledException)
            {
                Logger.Information("Whisper model {ModelId} download was canceled.", model.Id);
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to download Whisper model {ModelId}.", model.Id);
                throw;
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        private static async Task<bool> IsValidFileAsync(string path, WhisperModel model, CancellationToken cancellationToken)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != model.SizeBytes)
                return false;

            var actualHash = await VerifiedFileDownloader.ComputeSha256Async(path, cancellationToken);
            return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actualHash), Convert.FromHexString(model.Sha256));
        }

        private static void TryDeleteTemporaryFile(string temporaryPath)
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to delete the temporary Whisper model file.");
            }
        }
    }
}
