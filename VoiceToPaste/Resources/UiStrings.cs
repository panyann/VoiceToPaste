using System.Globalization;
using System.Resources;

namespace VoiceToPaste.Resources
{
    /// <summary>Wspólny dostęp do tekstów tworzonych poza zasobami formularzy WinForms.</summary>
    internal static class UiStrings
    {
        private static readonly ResourceManager ResourceManager = new(
            "VoiceToPaste.Resources.UiStrings",
            typeof(UiStrings).Assembly);

        public static string Get(string name, CultureInfo? culture = null)
        {
            var selectedCulture = culture ?? CultureInfo.CurrentUICulture;
            return ResourceManager.GetString(name, selectedCulture)
                ?? throw new MissingManifestResourceException($"UI text resource was not found: {name}.");
        }

        public static string Format(string name, params object?[] arguments)
        {
            return Format(name, CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture, arguments);
        }

        public static string Format(string name, CultureInfo culture, params object?[] arguments)
        {
            ArgumentNullException.ThrowIfNull(culture);
            return Format(name, culture, culture, arguments);
        }

        internal static string Format(
            string name,
            CultureInfo resourceCulture,
            CultureInfo formatCulture,
            params object?[] arguments)
        {
            return string.Format(formatCulture, Get(name, resourceCulture), arguments);
        }
    }
}
