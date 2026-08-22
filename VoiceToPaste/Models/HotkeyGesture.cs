using VoiceToPaste.Resources;

namespace VoiceToPaste.Models
{
    public enum HotkeyModifier
    {
        Control,
        Shift,
        Alt,
        Windows,
    }

    /// <summary>
    /// Czytelny, niezależny od Win32 opis skrótu zapisywany w settings.yaml.
    /// Brak skrótu reprezentuje wartość null w AppSettings.
    /// </summary>
    public sealed class HotkeyGesture
    {
        public string Key { get; set; } = "Space";

        public List<HotkeyModifier> Modifiers { get; set; } = [HotkeyModifier.Control];

        public static HotkeyGesture CreateDefault() => new();

        public bool IsValid(out string errorMessage)
        {
            if (!TryGetVirtualKey(out _))
            {
                errorMessage = UiStrings.Format("HotkeyKeyUnsupported", Key);
                return false;
            }

            if (Modifiers == null || Modifiers.Distinct().Count() != Modifiers.Count || Modifiers.Any(modifier => !Enum.IsDefined(modifier)))
            {
                errorMessage = UiStrings.Get("HotkeyModifiersInvalid");
                return false;
            }

            if (IsLetterOrDigitKey() && Modifiers.Count == 0)
            {
                errorMessage = UiStrings.Get("HotkeyLetterOrDigitRequiresModifier");
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        public bool TryGetVirtualKey(out uint virtualKey)
        {
            if (string.Equals(Key, "Space", StringComparison.OrdinalIgnoreCase))
            {
                virtualKey = 0x20;
                return true;
            }

            if (Key?.Length == 1 && char.IsAsciiLetterOrDigit(Key[0]))
            {
                virtualKey = char.ToUpperInvariant(Key[0]);
                return true;
            }

            if (Key?.Length is >= 2 and <= 3 && Key[0] is 'F' or 'f'
                && int.TryParse(Key[1..], out var functionKey)
                && functionKey is >= 1 and <= 24)
            {
                virtualKey = (uint)(0x70 + functionKey - 1);
                return true;
            }

            virtualKey = 0;
            return false;
        }

        public string ToDisplayString()
        {
            var parts = Modifiers.Select(modifier => modifier switch
            {
                HotkeyModifier.Control => "Ctrl",
                HotkeyModifier.Shift => "Shift",
                HotkeyModifier.Alt => "Alt",
                HotkeyModifier.Windows => "Win",
                _ => modifier.ToString(),
            }).ToList();
            parts.Add(Key);
            return string.Join(" + ", parts);
        }

        private bool IsLetterOrDigitKey() => Key?.Length == 1 && char.IsAsciiLetterOrDigit(Key[0]);
    }
}
