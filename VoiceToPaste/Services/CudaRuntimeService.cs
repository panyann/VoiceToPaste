using System.IO.Compression;
using Serilog;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Services
{
    /// <summary>Stan pobierania bibliotek CUDA przekazywany do formularza.</summary>
    public sealed record CudaRuntimeProgress(string Message, long DownloadedBytes = 0, long? TotalBytes = null);

    /// <summary>
    /// Pobiera z oficjalnego CDN NVIDIA wyłącznie biblioteki cuBLAS potrzebne przez backend CUDA
    /// Whisper.net. Adres, rozmiar i suma kontrolna są przypięte do sprawdzonego wydania, aby
    /// aplikacja nigdy nie pobierała automatycznie nieznanej wersji runtime.
    /// </summary>
    public sealed class CudaRuntimeService
    {
        private static readonly ILogger Logger = Log.ForContext<CudaRuntimeService>();
        private const string CublasDownloadUrl =
            "https://developer.download.nvidia.com/compute/cuda/redist/libcublas/windows-x86_64/libcublas-windows-x86_64-13.6.0.2-archive.zip";

        private const string CublasArchiveSha256 =
            "62E9FA30560C8F0A28E0CDCF9D6FC1FED347BCFAB8847239B9AE1FDC1D86408A";

        private const long CublasArchiveSizeBytes = 393_706_755;
        private const string CudartDownloadUrl =
            "https://developer.download.nvidia.com/compute/cuda/redist/cuda_cudart/windows-x86_64/cuda_cudart-windows-x86_64-13.3.29-archive.zip";
        private const string CudartArchiveSha256 =
            "1FEB7DD266813FFE8DBC24E115183A5AC35A4795C8D34ACA0DF85AB616B64D9C";
        private const long CudartArchiveSizeBytes = 2_589_792;
        private const string CublasFileName = "cublas64_13.dll";
        private const string CublasLtFileName = "cublasLt64_13.dll";
        private const string CudartFileName = "cudart64_13.dll";

        private static readonly VerifiedFileDownloader FileDownloader = new();
        private static readonly byte[] ExpectedCublasArchiveHash = CreateExpectedArchiveHash(CublasArchiveSha256);
        private static readonly byte[] ExpectedCudartArchiveHash = CreateExpectedArchiveHash(CudartArchiveSha256);

        private readonly string _applicationDirectory;
        private readonly string _runtimeDirectory;

        private sealed record CudaPackage(
            string Name,
            string DownloadUrl,
            long ArchiveSizeBytes,
            byte[] ExpectedHash,
            string TargetDirectory,
            string[] RequiredFiles);

        public CudaRuntimeService()
            : this(
                ApplicationPaths.Directory,
                Path.Combine(ApplicationPaths.Directory, "runtimes", "cuda", "win-x64"))
        {
        }

        // Konstruktor wykorzystywany przez testy, aby nie zapisywać plików obok aplikacji.
        internal CudaRuntimeService(string runtimeDirectory)
            : this(runtimeDirectory, runtimeDirectory)
        {
        }

        internal CudaRuntimeService(string applicationDirectory, string runtimeDirectory)
        {
            _applicationDirectory = applicationDirectory;
            _runtimeDirectory = runtimeDirectory;
        }

        /// <summary>Rozmiar oficjalnego archiwum wyświetlany użytkownikowi przed pobraniem.</summary>
        public static long DownloadSize => CublasArchiveSizeBytes + CudartArchiveSizeBytes;

        /// <summary>Łączny rozmiar komponentów, których brakuje w bieżącej instalacji.</summary>
        public long GetMissingDownloadSize() => GetMissingPackages().Sum(package => package.ArchiveSizeBytes);

        /// <summary>Sprawdza, czy komplet runtime CUDA wymagany przez Whisper.net jest dostępny.</summary>
        public bool IsRuntimeInstalled() =>
            File.Exists(Path.Combine(_runtimeDirectory, CublasFileName)) &&
            File.Exists(Path.Combine(_runtimeDirectory, CublasLtFileName)) &&
            File.Exists(Path.Combine(_applicationDirectory, CudartFileName));

        /// <summary>
        /// Pobiera, weryfikuje i instaluje CUDA Runtime. Operacja nie modyfikuje istniejących DLL,
        /// dopóki archiwum nie zostanie pobrane i poprawnie rozpakowane do katalogu tymczasowego.
        /// </summary>
        public async Task InstallAsync(
            IProgress<CudaRuntimeProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (IsRuntimeInstalled())
            {
                Logger.Information("CUDA libraries are already installed.");
                progress?.Report(new CudaRuntimeProgress(UiStrings.Get("CudaRuntimeAlreadyInstalled")));
                return;
            }

            var packages = GetMissingPackages();
            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "VoiceToPaste", Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(temporaryDirectory);
                foreach (var package in packages)
                {
                    var archivePath = Path.Combine(temporaryDirectory, $"{package.Name}.zip.part");
                    Logger.Information("Starting download of CUDA component {PackageName}. Expected size: {ExpectedBytes} B.",
                        package.Name,
                        package.ArchiveSizeBytes);

                    progress?.Report(new CudaRuntimeProgress(UiStrings.Format("CudaComponentDownloading", package.Name), 0, package.ArchiveSizeBytes));
                    await DownloadArchiveAsync(archivePath, package, progress, cancellationToken);
                    Logger.Information("Verified the SHA-256 hash of CUDA component {PackageName}.", package.Name);

                    progress?.Report(new CudaRuntimeProgress(UiStrings.Format("CudaComponentInstalling", package.Name)));
                    await InstallArchiveAsync(archivePath, temporaryDirectory, package, cancellationToken);
                }

                Logger.Information("CUDA libraries were installed.");
                progress?.Report(new CudaRuntimeProgress(UiStrings.Get("CudaRuntimeInstalled")));
            }
            catch (OperationCanceledException)
            {
                Logger.Information("CUDA download was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "CUDA library installation failed.");
                throw;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(temporaryDirectory))
                        Directory.Delete(temporaryDirectory, recursive: true);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Failed to delete temporary CUDA files.");
                }
            }
        }

        private static async Task DownloadArchiveAsync(
            string archivePath,
            CudaPackage package,
            IProgress<CudaRuntimeProgress>? progress,
            CancellationToken cancellationToken)
        {
            var lastLoggedPercent = -1;
            var progressAdapter = new Progress<VerifiedFileDownloadProgress>(download =>
            {
                progress?.Report(new CudaRuntimeProgress(
                    UiStrings.Format("CudaComponentDownloading", package.Name),
                    download.DownloadedBytes,
                    download.TotalBytes));

                var percent = (int)(download.DownloadedBytes * 100 / package.ArchiveSizeBytes);
                if (percent / 10 > lastLoggedPercent / 10)
                {
                    lastLoggedPercent = percent;
                    Logger.Information("Downloaded {DownloadedBytes} B ({Percent}%) of the CUDA archive.", download.DownloadedBytes, percent);
                }
            });

            var result = await FileDownloader.DownloadAsync(
                new VerifiedFileDownloadRequest(new Uri(package.DownloadUrl), package.ArchiveSizeBytes, package.ExpectedHash),
                archivePath,
                progressAdapter,
                cancellationToken);
            Logger.Information("CUDA download completed. SHA-256: {ActualSha256}.", Convert.ToHexString(result.Sha256));
        }

        private async Task InstallArchiveAsync(
            string archivePath,
            string temporaryDirectory,
            CudaPackage package,
            CancellationToken cancellationToken)
        {
            var stagingDirectory = Path.Combine(temporaryDirectory, "installed");
            Directory.CreateDirectory(stagingDirectory);
            Logger.Information("Extracting required CUDA libraries.");

            var installedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var targetFileName = GetTargetFileName(entry.FullName);
                if (targetFileName is null)
                    continue;

                if (!installedFiles.Add(targetFileName))
                    throw LocalizedExceptionFactory.InvalidData("CudaArchiveContainsDuplicateFile", targetFileName);

                var stagingPath = Path.Combine(stagingDirectory, targetFileName);
                await using var source = entry.Open();
                await using var destination = new FileStream(
                    stagingPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 1024 * 128,
                    useAsync: true);
                await source.CopyToAsync(destination, cancellationToken);
            }

            var requiredFiles = package.RequiredFiles;
            var missingFiles = requiredFiles.Where(fileName => !installedFiles.Contains(fileName)).ToArray();
            if (missingFiles.Length > 0)
            {
                Logger.Error("CUDA archive does not contain the required libraries: {MissingFiles}.", string.Join(", ", missingFiles));
                throw LocalizedExceptionFactory.InvalidData("CudaArchiveMissingLibraries", string.Join(", ", missingFiles));
            }

            Directory.CreateDirectory(package.TargetDirectory);
            foreach (var fileName in requiredFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(
                    Path.Combine(stagingDirectory, fileName),
                    Path.Combine(package.TargetDirectory, fileName),
                    overwrite: true);
            }

            Logger.Information("Installed all required CUDA libraries.");
        }

        // Wybieramy wyłącznie nazwy plików, których faktycznie potrzebuje backend. Nie używamy
        // ścieżek z ZIP-a, dzięki czemu złośliwy wpis nie może zapisać danych poza katalogiem runtime.
        internal static string? GetTargetFileName(string archiveEntryPath)
        {
            var fileName = Path.GetFileName(archiveEntryPath);
            if (string.Equals(fileName, CublasFileName, StringComparison.OrdinalIgnoreCase))
                return CublasFileName;
            if (string.Equals(fileName, CublasLtFileName, StringComparison.OrdinalIgnoreCase))
                return CublasLtFileName;
            if (string.Equals(fileName, CudartFileName, StringComparison.OrdinalIgnoreCase))
                return CudartFileName;
            return null;
        }

        internal static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken = default)
        {
            return await VerifiedFileDownloader.ComputeSha256Async(filePath, cancellationToken);
        }

        internal static int ExpectedArchiveHashLength => ExpectedCublasArchiveHash.Length;

        private IReadOnlyList<CudaPackage> GetMissingPackages()
        {
            var packages = new List<CudaPackage>();
            if (!File.Exists(Path.Combine(_runtimeDirectory, CublasFileName)) ||
                !File.Exists(Path.Combine(_runtimeDirectory, CublasLtFileName)))
            {
                packages.Add(new CudaPackage(
                    "cuBLAS",
                    CublasDownloadUrl,
                    CublasArchiveSizeBytes,
                    ExpectedCublasArchiveHash,
                    _runtimeDirectory,
                    [CublasFileName, CublasLtFileName]));
            }

            if (!File.Exists(Path.Combine(_applicationDirectory, CudartFileName)))
            {
                packages.Add(new CudaPackage(
                    "CUDA Runtime",
                    CudartDownloadUrl,
                    CudartArchiveSizeBytes,
                    ExpectedCudartArchiveHash,
                    _applicationDirectory,
                    [CudartFileName]));
            }

            return packages;
        }

        private static byte[] CreateExpectedArchiveHash(string archiveSha256)
        {
            var hash = Convert.FromHexString(archiveSha256);
            if (hash.Length != 32)
                throw LocalizedExceptionFactory.InvalidOperation("CudaArchiveSha256LengthInvalid");

            return hash;
        }
    }
}
