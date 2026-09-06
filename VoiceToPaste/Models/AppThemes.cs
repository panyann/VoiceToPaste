namespace VoiceToPaste.Models
{
    /// <summary>
    /// Supported application UI themes. The theme comes exclusively from application
    /// settings and is fully independent of the Windows system theme.
    /// </summary>
    public static class AppThemes
    {
        public const string Dark = "dark";
        public const string Light = "light";

        /// <summary>
        /// Normalizes a value from settings: trimmed, case-insensitive comparison.
        /// An unknown, empty, or null value falls back to the dark theme, which is
        /// the application default.
        /// </summary>
        public static string Normalize(string? selectedTheme)
        {
            var normalizedTheme = selectedTheme?.Trim();
            if (string.Equals(normalizedTheme, Dark, StringComparison.OrdinalIgnoreCase))
            {
                return Dark;
            }

            if (string.Equals(normalizedTheme, Light, StringComparison.OrdinalIgnoreCase))
            {
                return Light;
            }

            return Dark;
        }
    }
}
