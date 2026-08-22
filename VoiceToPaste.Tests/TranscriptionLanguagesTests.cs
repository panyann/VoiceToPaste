using System.Globalization;
using VoiceToPaste.Models;

namespace VoiceToPaste.Tests
{
    public sealed class TranscriptionLanguagesTests
    {
        [Fact]
        public void SupportedCodes_ContainsAllLanguagesFromWhisperNet()
        {
            Assert.Equal(100, TranscriptionLanguages.SupportedCodes.Count);
            Assert.Contains("pl", TranscriptionLanguages.SupportedCodes);
            Assert.Contains("en", TranscriptionLanguages.SupportedCodes);
            Assert.Contains("yue", TranscriptionLanguages.SupportedCodes);
        }

        [Theory]
        [InlineData("en-US", "en")]
        [InlineData("fil", "tl")]
        [InlineData("jv", "jw")]
        [InlineData("nb", "no")]
        [InlineData("auto", "auto")]
        public void Normalize_ConvertsSystemAndWhisperCodes(string code, string expected)
        {
            Assert.Equal(expected, TranscriptionLanguages.Normalize(code));
        }

        [Fact]
        public void Normalize_UnknownCode_UsesSpecifiedSystemCulture()
        {
            var englishCulture = CultureInfo.GetCultureInfo("en-US");

            var normalized = TranscriptionLanguages.Normalize("unknown", englishCulture);

            Assert.Equal("en", normalized);
        }

        [Fact]
        public void GetSystemDefaultCode_UsesSupportedSystemLanguage()
        {
            var koreanCulture = CultureInfo.GetCultureInfo("ko-KR");

            Assert.Equal("ko", TranscriptionLanguages.GetSystemDefaultCode(koreanCulture));
        }

        [Fact]
        public void GetSystemDefaultCode_UsesAutoDetectionForUnsupportedSystemLanguage()
        {
            var gaelicCulture = CultureInfo.GetCultureInfo("gd-GB");

            Assert.Equal(TranscriptionLanguages.AutoDetectCode, TranscriptionLanguages.GetSystemDefaultCode(gaelicCulture));
        }

        [Fact]
        public void GetOptions_PutsAutoAndSystemLanguageBeforeSortedRemainingLanguages()
        {
            var options = TranscriptionLanguages.GetOptions(CultureInfo.GetCultureInfo("pl-PL"));

            Assert.Equal(101, options.Count);
            Assert.Equal(TranscriptionLanguages.AutoDetectCode, options[0].Code);
            Assert.Equal("pl", options[1].Code);
            Assert.Equal(101, options.Select(option => option.Code).Distinct().Count());

            var remainingNames = options.Skip(2).Select(option => option.DisplayName).ToList();
            var expectedNames = remainingNames.Order(StringComparer.CurrentCultureIgnoreCase).ToList();
            Assert.Equal(expectedNames, remainingNames);
        }
    }
}
