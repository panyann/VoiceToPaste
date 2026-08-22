using System.Globalization;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Models
{
    /// <summary>Pozycja listy języków: kod dla aplikacji i nazwa własna dla użytkownika.</summary>
    public sealed record TranscriptionLanguageOption(string Code, string DisplayName);

    /// <summary>
    /// Kody języków obsługiwane przez whisper.cpp używany przez Whisper.net 1.9.1.
    /// Kod <c>auto</c> oznacza automatyczne wykrywanie języka, a nie język modelu.
    /// </summary>
    public static class TranscriptionLanguages
    {
        public const string AutoDetectCode = "auto";

        private static readonly HashSet<string> SupportedLanguageCodes = new(StringComparer.Ordinal)
        {
            "af", "am", "ar", "as", "az", "ba", "be", "bg", "bn", "bo",
            "br", "bs", "ca", "cs", "cy", "da", "de", "el", "en", "es",
            "et", "eu", "fa", "fi", "fo", "fr", "gl", "gu", "ha", "haw",
            "he", "hi", "hr", "ht", "hu", "hy", "id", "is", "it", "ja",
            "jw", "ka", "kk", "km", "kn", "ko", "la", "lb", "ln", "lo",
            "lt", "lv", "mg", "mi", "mk", "ml", "mn", "mr", "ms", "mt",
            "my", "ne", "nl", "nn", "no", "oc", "pa", "pl", "ps", "pt",
            "ro", "ru", "sa", "sd", "si", "sk", "sl", "sn", "so", "sq",
            "sr", "su", "sv", "sw", "ta", "te", "tg", "th", "tk", "tl",
            "tr", "tt", "uk", "ur", "uz", "vi", "yi", "yo", "yue", "zh",
        };

        private static readonly Dictionary<string, string> SystemCodeAliases = new(StringComparer.Ordinal)
        {
            // whisper.cpp zachowuje historyczne kody dla tych języków.
            ["fil"] = "tl",
            ["jv"] = "jw",
            ["nb"] = "no",
        };

        private static readonly Dictionary<string, string> NativeNameOverrides = new(StringComparer.Ordinal)
        {
            // Niektóre historyczne kody whisper.cpp nie mają neutralnej kultury .NET.
            ["ht"] = "Kreyòl ayisyen",
            ["jw"] = "Basa Jawa",
            ["su"] = "Basa Sunda",
            ["tl"] = "Tagalog",
            ["yue"] = "粵語",
        };

        /// <summary>Obsługiwane kody językowe, bez technicznej wartości <c>auto</c>.</summary>
        public static IReadOnlySet<string> SupportedCodes => SupportedLanguageCodes;

        /// <summary>
        /// Buduje listę dla interfejsu z nazwami własnymi języków. Automatyczne wykrywanie
        /// jest pierwsze, język systemowy drugi, a pozostałe pozycje są sortowane alfabetycznie.
        /// </summary>
        public static IReadOnlyList<TranscriptionLanguageOption> GetOptions(CultureInfo? systemCulture = null)
        {
            var systemCode = GetSystemDefaultCode(systemCulture);
            var languageOptions = SupportedLanguageCodes
                .Select(code => new TranscriptionLanguageOption(code, GetNativeName(code)))
                .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var options = new List<TranscriptionLanguageOption>
            {
                new(AutoDetectCode, UiStrings.Get("TranscriptionLanguageAutoDetect"))
            };

            var systemLanguage = languageOptions.FirstOrDefault(option => option.Code == systemCode);
            if (systemLanguage != null)
            {
                options.Add(systemLanguage);
                languageOptions.Remove(systemLanguage);
            }

            options.AddRange(languageOptions);
            return options;
        }

        /// <summary>
        /// Zwraca kod języka interfejsu systemu, jeśli model go obsługuje; w przeciwnym
        /// razie wybiera automatyczne wykrywanie, aby nie wymuszać niepowiązanego języka.
        /// </summary>
        public static string GetSystemDefaultCode(CultureInfo? culture = null)
        {
            var systemCode = (culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;
            return FindSupportedCode(systemCode) ?? AutoDetectCode;
        }

        /// <summary>
        /// Normalizuje kod zapisany w ustawieniach. Akceptuje także tagi kultury, np.
        /// <c>en-US</c>. Nieprawidłowa wartość wraca do języka systemowego.
        /// </summary>
        public static string Normalize(string? languageCode, CultureInfo? fallbackCulture = null)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return GetSystemDefaultCode(fallbackCulture);

            var normalizedCode = languageCode.Trim().ToLowerInvariant();
            var supportedCode = FindSupportedCode(normalizedCode);
            if (supportedCode != null)
                return supportedCode;

            try
            {
                var cultureCode = CultureInfo.GetCultureInfo(normalizedCode).TwoLetterISOLanguageName;
                return FindSupportedCode(cultureCode) ?? GetSystemDefaultCode(fallbackCulture);
            }
            catch (CultureNotFoundException)
            {
                return GetSystemDefaultCode(fallbackCulture);
            }
        }

        private static string? FindSupportedCode(string languageCode)
        {
            if (string.Equals(languageCode, AutoDetectCode, StringComparison.Ordinal))
                return AutoDetectCode;

            if (SystemCodeAliases.TryGetValue(languageCode, out var alias))
                return alias;

            return SupportedLanguageCodes.Contains(languageCode) ? languageCode : null;
        }

        private static string GetNativeName(string languageCode)
        {
            if (NativeNameOverrides.TryGetValue(languageCode, out var nativeName))
                return nativeName;

            return CultureInfo.GetCultureInfo(languageCode).NativeName;
        }
    }
}
