using VoiceToPaste.Services;

namespace VoiceToPaste.Tests
{
    public sealed class TranscriptionOutputTests
    {
        [Fact]
        public void InputStructureSize_MatchesNativeWin32Layout()
        {
            var expectedSize = IntPtr.Size == 8 ? 40 : 28;

            Assert.Equal(expectedSize, TranscriptionOutput.InputStructureSize);
        }
    }
}
