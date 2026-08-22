using System.Globalization;
using VoiceToPaste.Models;

namespace VoiceToPaste.Tests
{
    public sealed class WhisperModelCatalogTests
    {
        [Fact]
        public void GetAll_ReturnsSixMultilingualModels()
        {
            var models = WhisperModelCatalog.GetAll();

            Assert.Equal(
                ["tiny", "base", "small", "medium", "large-v3-turbo", "large-v3"],
                models.Select(model => model.Id));
            Assert.All(models, model =>
            {
                Assert.DoesNotContain(".en", model.FileName, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith("https://huggingface.co/ggerganov/whisper.cpp/resolve/main/", model.DownloadUri.AbsoluteUri);
                Assert.Equal(64, model.Sha256.Length);
                Assert.True(model.SizeBytes > 0);
            });
        }

        [Fact]
        public void GetById_DefaultModel_ReturnsLargeV3()
        {
            var model = WhisperModelCatalog.GetById(WhisperModelCatalog.DefaultModelId);

            Assert.Equal("large-v3", model.Id);
            Assert.Equal("ggml-large-v3.bin", model.FileName);
        }

        [Fact]
        public void GetById_UnknownModel_Throws()
        {
            Assert.Null(WhisperModelCatalog.TryGetById("unknown"));
            Assert.Throws<ArgumentException>(() => WhisperModelCatalog.GetById("unknown"));
        }

        [Fact]
        public void GetDescription_ReturnsTextForPolishAndEnglishCultures()
        {
            var model = WhisperModelCatalog.GetById("tiny");

            var polishDescription = model.GetDescription(CultureInfo.GetCultureInfo("pl"));
            var englishDescription = model.GetDescription(CultureInfo.GetCultureInfo("en"));

            Assert.Equal("Najszybszy, najniższa jakość rozpoznawania.", polishDescription);
            Assert.Equal("The fastest model with the lowest recognition quality.", englishDescription);
        }

    }
}
