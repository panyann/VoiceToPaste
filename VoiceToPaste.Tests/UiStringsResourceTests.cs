using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using VoiceToPaste.Models;

namespace VoiceToPaste.Tests
{
    public sealed partial class UiStringsResourceTests
    {
        private const string ResourceBaseName = "VoiceToPaste.Resources.UiStrings";

        [Fact]
        public void PolishAndNeutralEnglishResources_HaveMatchingNonEmptyKeysAndPlaceholders()
        {
            var resourceManager = new ResourceManager(ResourceBaseName, typeof(UiLanguages).Assembly);
            var polishResources = ReadResources(resourceManager, CultureInfo.GetCultureInfo("pl"));
            var englishResources = ReadResources(resourceManager, CultureInfo.InvariantCulture);

            Assert.Equal(polishResources.Keys.Order(), englishResources.Keys.Order());
            Assert.All(polishResources.Values, value => Assert.False(string.IsNullOrWhiteSpace(value)));
            Assert.All(englishResources.Values, value => Assert.False(string.IsNullOrWhiteSpace(value)));

            foreach (var key in polishResources.Keys)
            {
                Assert.Equal(
                    GetPlaceholders(polishResources[key]),
                    GetPlaceholders(englishResources[key]));
            }
        }

        private static Dictionary<string, string> ReadResources(ResourceManager resourceManager, CultureInfo culture)
        {
            var resourceSet = resourceManager.GetResourceSet(culture, true, false);
            Assert.NotNull(resourceSet);

            var resources = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in resourceSet)
            {
                if (entry.Key is string key && entry.Value is string value)
                {
                    resources.Add(key, value);
                }
            }

            return resources;
        }

        private static string[] GetPlaceholders(string value)
        {
            return PlaceholderPattern()
                .Matches(value)
                .Select(match => match.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        [GeneratedRegex(@"\{\d+(?:[^}]*)?\}")]
        private static partial Regex PlaceholderPattern();
    }
}
