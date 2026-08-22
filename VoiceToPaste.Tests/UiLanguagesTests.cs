using System.Globalization;
using VoiceToPaste.Models;

namespace VoiceToPaste.Tests
{
    public sealed class UiLanguagesTests
    {
        [Theory]
        [InlineData("pl-PL", UiLanguages.PolishCode)]
        [InlineData("en-US", UiLanguages.EnglishCode)]
        [InlineData("de-DE", UiLanguages.EnglishCode)]
        public void GetSystemDefaultCode_UsesPolishOnlyForPolishWindows(string cultureName, string expected)
        {
            var result = UiLanguages.GetSystemDefaultCode(CultureInfo.GetCultureInfo(cultureName));

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("PL", UiLanguages.PolishCode)]
        [InlineData(" en ", UiLanguages.EnglishCode)]
        [InlineData("unsupported", UiLanguages.PolishCode)]
        public void Normalize_ReturnsSupportedCodeOrSystemDefault(string input, string expected)
        {
            var result = UiLanguages.Normalize(input, CultureInfo.GetCultureInfo("pl-PL"));

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(UiLanguages.PolishCode, "pl")]
        [InlineData(UiLanguages.EnglishCode, "en")]
        public void GetCulture_ReturnsCultureForSupportedLanguage(string languageCode, string expectedCulture)
        {
            Assert.Equal(expectedCulture, UiLanguages.GetCulture(languageCode).TwoLetterISOLanguageName);
        }

        [Fact]
        public void GetOptions_ReturnsBothLanguagesWithNativeNames()
        {
            var options = UiLanguages.GetOptions();

            Assert.Collection(
                options,
                polish =>
                {
                    Assert.Equal(UiLanguages.PolishCode, polish.Code);
                    Assert.Equal("Polski", polish.DisplayName);
                },
                english =>
                {
                    Assert.Equal(UiLanguages.EnglishCode, english.Code);
                    Assert.Equal("English", english.DisplayName);
                });
        }
    }
}
