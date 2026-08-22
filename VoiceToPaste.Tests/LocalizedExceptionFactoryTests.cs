using System.Globalization;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Tests
{
    public sealed class LocalizedExceptionFactoryTests
    {
        private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en");
        private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl");

        [Fact]
        public void InvalidOperation_AlwaysUsesEnglishMessage()
        {
            var exception = LocalizedExceptionFactory.InvalidOperation("KeywordRowIncomplete", 7);

            Assert.Equal(UiStrings.Format("KeywordRowIncomplete", EnglishCulture, 7), exception.Message);
            Assert.NotEqual(UiStrings.Format("KeywordRowIncomplete", PolishCulture, 7), exception.Message);
        }

        [Fact]
        public void GetUserMessage_FormatsMarkedExceptionForRequestedCulture()
        {
            var exception = LocalizedExceptionFactory.InvalidData("UnexpectedServerFileSize", 12_345, 67_890);

            Assert.Equal(
                UiStrings.Format("UnexpectedServerFileSize", PolishCulture, 12_345, 67_890),
                LocalizedExceptionFactory.GetUserMessage(exception, PolishCulture));
            Assert.Equal(
                UiStrings.Format("UnexpectedServerFileSize", EnglishCulture, 12_345, 67_890),
                LocalizedExceptionFactory.GetUserMessage(exception, EnglishCulture));
        }

        [Fact]
        public void GetUserMessage_ReturnsOriginalMessageForExternalException()
        {
            var exception = new IOException("Library error");

            Assert.Equal("Library error", LocalizedExceptionFactory.GetUserMessage(exception, PolishCulture));
        }
    }
}
