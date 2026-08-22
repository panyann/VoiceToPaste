using System.Collections;
using System.ComponentModel;
using System.Globalization;
using VoiceToPaste.Forms;

namespace VoiceToPaste.Tests
{
    public sealed class FormLocalizationResourceTests
    {
        private static readonly Type[] LocalizedFormTypes =
        [
            typeof(SettingsForm),
            typeof(TesterForm),
            typeof(KeyWordsForm),
            typeof(WhisperModelDownloadForm),
            typeof(CudaRuntimeDownloadForm),
        ];

        private static readonly HashSet<string> LayoutPropertyNames = new(StringComparer.Ordinal)
        {
            "Anchor",
            "AutoScaleDimensions",
            "AutoScaleMode",
            "AutoSize",
            "ClientSize",
            "Dock",
            "Font",
            "Location",
            "Margin",
            "MaximumSize",
            "MinimumSize",
            "Padding",
            "Size",
        };

        [Theory]
        [InlineData(typeof(SettingsForm), "labelUiLanguage.Text", "Interface language:")]
        [InlineData(typeof(TesterForm), "$this.Text", "Transcription tester")]
        [InlineData(typeof(KeyWordsForm), "btnSave.Text", "Save changes")]
        [InlineData(typeof(WhisperModelDownloadForm), "$this.Text", "Download Whisper model")]
        [InlineData(typeof(CudaRuntimeDownloadForm), "$this.Text", "Install NVIDIA CUDA GPU support")]
        public void NeutralResources_ReturnExpectedEnglishText(Type formType, string resourceName, string expected)
        {
            var resources = new ComponentResourceManager(formType);
            var value = resources.GetString(resourceName, CultureInfo.InvariantCulture);

            Assert.Equal(expected, value);
        }

        [Fact]
        public void PolishResources_DoNotOverrideFormLayout()
        {
            var polishCulture = CultureInfo.GetCultureInfo("pl");

            foreach (var formType in LocalizedFormTypes)
            {
                var resources = new ComponentResourceManager(formType);
                var resourceSet = resources.GetResourceSet(polishCulture, true, false);

                Assert.NotNull(resourceSet);
                foreach (DictionaryEntry entry in resourceSet)
                {
                    var resourceName = Assert.IsType<string>(entry.Key);
                    Assert.False(
                        IsLayoutResource(resourceName),
                        $"Polish resources for {formType.Name} override layout property {resourceName}.");
                }
            }
        }

        private static bool IsLayoutResource(string resourceName)
        {
            var separatorIndex = resourceName.LastIndexOf('.');
            if (separatorIndex < 0 || separatorIndex == resourceName.Length - 1)
            {
                return false;
            }

            var propertyName = resourceName[(separatorIndex + 1)..];
            return LayoutPropertyNames.Contains(propertyName);
        }

        [Theory]
        [InlineData(typeof(SettingsForm), "labelUiLanguage.Text", "Język UI:")]
        [InlineData(typeof(TesterForm), "$this.Text", "Tester transkrypcji")]
        [InlineData(typeof(KeyWordsForm), "btnSave.Text", "Zapisz zmiany")]
        [InlineData(typeof(WhisperModelDownloadForm), "$this.Text", "Pobieranie modelu Whisper")]
        [InlineData(typeof(CudaRuntimeDownloadForm), "$this.Text", "Instalacja NVIDIA CUDA do obsługi GPU")]
        public void PolishResources_ReturnExpectedText(Type formType, string resourceName, string expected)
        {
            var resources = new ComponentResourceManager(formType);

            var value = resources.GetString(resourceName, CultureInfo.GetCultureInfo("pl"));

            Assert.Equal(expected, value);
        }

        [Fact]
        public void MissingPolishResource_FallsBackToNeutralEnglishResource()
        {
            var resources = new ComponentResourceManager(typeof(SettingsForm));
            var polishCulture = CultureInfo.GetCultureInfo("pl");
            var polishResources = resources.GetResourceSet(polishCulture, true, false);

            Assert.NotNull(polishResources);
            Assert.Null(polishResources.GetObject(">>btnTester.Name"));
            Assert.Equal("btnTester", resources.GetString(">>btnTester.Name", polishCulture));
        }
    }
}
