using System.Text.RegularExpressions;
using VoiceToPaste.Models;

namespace VoiceToPaste.Services
{
    /// <summary>Wynik korekty tekstu wraz z liczbą faktycznie dopasowanych fraz.</summary>
    internal readonly record struct KeywordReplacementResult(string Text, int ReplacementCount);

    /// <summary>
    /// Podmienia skonfigurowane pełne frazy w końcowym tekście transkrypcji.
    /// Jedno wyrażenie regularne pracuje na tekście wejściowym, dzięki czemu wynik
    /// jednej reguły nie jest ponownie przetwarzany przez kolejne reguły.
    /// </summary>
    internal sealed class KeywordReplacementService
    {
        private const string WordCharacterPattern = @"\p{L}\p{N}_";

        /// <summary>
        /// Stosuje wszystkie poprawne reguły jednocześnie, bez podmieniania fragmentów słów
        /// i bez ponownego przetwarzania tekstu wstawionego przez dopasowaną regułę.
        /// </summary>
        public KeywordReplacementResult Replace(string text, IEnumerable<DGV_KeyWords> keywords)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(keywords);

            var replacements = Normalize(keywords);
            if (text.Length == 0 || replacements.Count == 0)
                return new KeywordReplacementResult(text, 0);

            // Dłuższe frazy muszą znaleźć się wcześniej, aby np. „plik agent” wygrał z „plik”.
            var alternatives = replacements.Keys
                .OrderByDescending(key => key.Length)
                .Select(Regex.Escape);
            var pattern = $@"(?<![{WordCharacterPattern}])(?:{string.Join("|", alternatives)})(?![{WordCharacterPattern}])";

            var replacementCount = 0;
            var replacedText = Regex.Replace(
                text,
                pattern,
                match =>
                {
                    replacementCount++;
                    return replacements[match.Value];
                },
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return new KeywordReplacementResult(replacedText, replacementCount);
        }

        private static Dictionary<string, string> Normalize(IEnumerable<DGV_KeyWords> keywords)
        {
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var keyword in keywords)
            {
                if (keyword == null)
                    continue;

                var key = keyword.Key?.Trim();
                var word = keyword.Word?.Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(word))
                    continue;

                // Formularz blokuje duplikaty; pierwszy wpis pozostaje bezpiecznym wyborem
                // również dla ustawień zmodyfikowanych ręcznie w pliku YAML.
                replacements.TryAdd(key, word);
            }

            return replacements;
        }
    }
}
