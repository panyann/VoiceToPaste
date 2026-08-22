using System.Globalization;

namespace VoiceToPaste.Models
{
    /// <summary>Pozycja listy języków interfejsu z trwałym kodem i nazwą własną.</summary>
    public sealed record UiLanguageOption(string Code, string DisplayName);

    /// <summary>
    /// Obsługiwane języki interfejsu oraz reguły wyboru kultury aplikacji.
    /// Język interfejsu pozostaje niezależny od języka rozpoznawania Whispera.
    /// </summary>
    public static class UiLanguages
    {
        public const string PolishCode = "pl";
        public const string EnglishCode = "en";

        private static readonly IReadOnlyList<UiLanguageOption> LanguageOptions =
        [
            new(PolishCode, "Polski"),
            new(EnglishCode, "English"),
        ];

        public static IReadOnlyList<UiLanguageOption> GetOptions() => LanguageOptions;

        /// <summary>
        /// Wybiera polski wyłącznie dla polskiego języka interfejsu Windows;
        /// pozostałe kultury otrzymują angielski interfejs aplikacji.
        /// </summary>
        public static string GetSystemDefaultCode(CultureInfo? systemCulture = null)
        {
            var culture = systemCulture ?? CultureInfo.CurrentUICulture;
            if (string.Equals(
                culture.TwoLetterISOLanguageName,
                PolishCode,
                StringComparison.OrdinalIgnoreCase))
            {
                return PolishCode;
            }

            return EnglishCode;
        }

        /// <summary>
        /// Normalizuje wartość z ustawień. Nieznany język wraca do wyboru wynikającego
        /// z języka interfejsu Windows, aby aplikacja zawsze miała komplet zasobów.
        /// </summary>
        public static string Normalize(string? languageCode, CultureInfo? systemCulture = null)
        {
            var normalizedLanguageCode = languageCode?.Trim();
            if (string.Equals(normalizedLanguageCode, PolishCode, StringComparison.OrdinalIgnoreCase))
            {
                return PolishCode;
            }

            if (string.Equals(normalizedLanguageCode, EnglishCode, StringComparison.OrdinalIgnoreCase))
            {
                return EnglishCode;
            }

            return GetSystemDefaultCode(systemCulture);
        }

        /// <summary>Zwraca konkretną kulturę używaną przez zasoby i formatowanie interfejsu.</summary>
        public static CultureInfo GetCulture(string languageCode) =>
            CultureInfo.GetCultureInfo(Normalize(languageCode));

        /// <summary>
        /// Ustawia kulturę bieżącego wątku i domyślną kulturę kolejnych wątków przed
        /// utworzeniem interfejsu, aby formularze i komunikaty tła używały tych samych zasobów.
        /// </summary>
        public static void ApplyCulture(string languageCode)
        {
            var culture = GetCulture(languageCode);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
    }
}
