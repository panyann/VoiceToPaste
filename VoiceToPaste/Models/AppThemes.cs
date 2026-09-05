namespace VoiceToPaste.Models
{
    /// <summary>
    /// Obsługiwane motywy interfejsu aplikacji. Motyw pochodzi wyłącznie z ustawień
    /// i jest całkowicie niezależny od motywu systemowego Windows.
    /// </summary>
    public static class AppThemes
    {
        public const string Dark = "dark";
        public const string Light = "light";

        /// <summary>
        /// Normalizuje wartość z ustawień: trim i porównanie bez rozróżnienia wielkości
        /// liter. Nieznana, pusta lub null wartość wraca do motywu ciemnego,
        /// który jest motywem domyślnym aplikacji.
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
