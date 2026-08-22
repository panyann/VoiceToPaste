using VoiceToPaste.Services;

namespace VoiceToPaste.Tests
{
    /// <summary>Testy logiki runtime CUDA bez pobierania dużego archiwum NVIDIA.</summary>
    public sealed class CudaRuntimeServiceTests : IDisposable
    {
        private readonly string _tempDirectory;

        public CudaRuntimeServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "VoiceToPaste.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        [Fact]
        public void IsRuntimeInstalled_MissingLibrary_ReturnsFalse()
        {
            var service = new CudaRuntimeService(_tempDirectory);
            File.WriteAllText(Path.Combine(_tempDirectory, "cublas64_13.dll"), string.Empty);

            Assert.False(service.IsRuntimeInstalled());
        }

        [Fact]
        public void IsRuntimeInstalled_RequiredLibrariesPresent_ReturnsTrue()
        {
            var service = new CudaRuntimeService(_tempDirectory);
            File.WriteAllText(Path.Combine(_tempDirectory, "cublas64_13.dll"), string.Empty);
            File.WriteAllText(Path.Combine(_tempDirectory, "cublasLt64_13.dll"), string.Empty);

            Assert.False(service.IsRuntimeInstalled());

            File.WriteAllText(Path.Combine(_tempDirectory, "cudart64_13.dll"), string.Empty);

            Assert.True(service.IsRuntimeInstalled());
            Assert.Equal(0, service.GetMissingDownloadSize());
        }

        [Fact]
        public void GetMissingDownloadSize_OnlyCudartMissing_ReturnsCudartPackageSize()
        {
            var service = new CudaRuntimeService(_tempDirectory);
            File.WriteAllText(Path.Combine(_tempDirectory, "cublas64_13.dll"), string.Empty);
            File.WriteAllText(Path.Combine(_tempDirectory, "cublasLt64_13.dll"), string.Empty);

            Assert.Equal(2_589_792, service.GetMissingDownloadSize());
        }

        [Theory]
        [InlineData("lib/x64/cublas64_13.dll", "cublas64_13.dll")]
        [InlineData("lib/x64/cublasLt64_13.dll", "cublasLt64_13.dll")]
        [InlineData("bin/x64/cudart64_13.dll", "cudart64_13.dll")]
        [InlineData("licenses/LICENSE", null)]
        [InlineData("licenses/LICENSE.txt", null)]
        [InlineData("lib/x64/other.dll", null)]
        public void GetTargetFileName_SelectsOnlyExpectedArchiveFiles(string archivePath, string? expectedFileName)
        {
            Assert.Equal(expectedFileName, CudaRuntimeService.GetTargetFileName(archivePath));
        }

        [Fact]
        public async Task ComputeSha256Async_ReturnsExpectedHash()
        {
            var filePath = Path.Combine(_tempDirectory, "checksum.txt");
            await File.WriteAllTextAsync(filePath, "abc");

            var hash = await CudaRuntimeService.ComputeSha256Async(filePath);

            Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
        }

        [Fact]
        public void ExpectedArchiveHash_HasSha256Length()
        {
            Assert.Equal(32, CudaRuntimeService.ExpectedArchiveHashLength);
        }
    }
}
