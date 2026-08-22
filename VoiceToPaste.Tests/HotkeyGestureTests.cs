using VoiceToPaste.Models;

namespace VoiceToPaste.Tests
{
    public sealed class HotkeyGestureTests
    {
        [Fact]
        public void CreateDefault_ReturnsCtrlSpace()
        {
            var hotkey = HotkeyGesture.CreateDefault();

            Assert.Equal("Space", hotkey.Key);
            Assert.Equal([HotkeyModifier.Control], hotkey.Modifiers);
            Assert.True(hotkey.IsValid(out _));
            Assert.Equal("Ctrl + Space", hotkey.ToDisplayString());
        }

        [Theory]
        [InlineData("A", 0x41u)]
        [InlineData("5", 0x35u)]
        [InlineData("F12", 0x7Bu)]
        [InlineData("Space", 0x20u)]
        public void TryGetVirtualKey_SupportedKey_ReturnsWin32VirtualKey(string key, uint expectedVirtualKey)
        {
            var hotkey = new HotkeyGesture { Key = key };

            var isSupported = hotkey.TryGetVirtualKey(out var virtualKey);

            Assert.True(isSupported);
            Assert.Equal(expectedVirtualKey, virtualKey);
        }

        [Fact]
        public void IsValid_LetterWithoutModifier_ReturnsFalse()
        {
            var hotkey = new HotkeyGesture { Key = "A", Modifiers = [] };

            var isValid = hotkey.IsValid(out var errorMessage);

            Assert.False(isValid);
            Assert.False(string.IsNullOrWhiteSpace(errorMessage));
        }

        [Fact]
        public void IsValid_DuplicateModifier_ReturnsFalse()
        {
            var hotkey = new HotkeyGesture
            {
                Key = "Space",
                Modifiers = [HotkeyModifier.Control, HotkeyModifier.Control],
            };

            var isValid = hotkey.IsValid(out var errorMessage);

            Assert.False(isValid);
            Assert.False(string.IsNullOrWhiteSpace(errorMessage));
        }

        [Fact]
        public void IsValid_UnknownModifier_ReturnsFalse()
        {
            var hotkey = new HotkeyGesture
            {
                Key = "Space",
                Modifiers = [(HotkeyModifier)99],
            };

            var isValid = hotkey.IsValid(out var errorMessage);

            Assert.False(isValid);
            Assert.False(string.IsNullOrWhiteSpace(errorMessage));
        }

        [Fact]
        public void IsValid_UnsupportedKey_ReturnsFalse()
        {
            var hotkey = new HotkeyGesture { Key = "Escape" };

            var isValid = hotkey.IsValid(out var errorMessage);

            Assert.False(isValid);
            Assert.Contains("Escape", errorMessage, StringComparison.Ordinal);
        }
    }
}
