using VoiceToPaste.Models;
using VoiceToPaste.Services;

namespace VoiceToPaste.Tests
{
    public sealed class KeywordReplacementServiceTests
    {
        private readonly KeywordReplacementService _service = new();

        [Fact]
        public void Replace_MatchingPhrase_IgnoresLetterCase()
        {
            var keywords = CreateKeywords(("plik agent", "AGENTS.md"));

            var result = _service.Replace("Otwórz PLIK AGENT.", keywords);

            Assert.Equal("Otwórz AGENTS.md.", result.Text);
            Assert.Equal(1, result.ReplacementCount);
        }

        [Fact]
        public void Replace_PhraseNextToPunctuation_PreservesPunctuation()
        {
            var keywords = CreateKeywords(("plik agent", "AGENTS.md"));

            var result = _service.Replace("(plik agent), potem: plik agent!", keywords);

            Assert.Equal("(AGENTS.md), potem: AGENTS.md!", result.Text);
            Assert.Equal(2, result.ReplacementCount);
        }

        [Fact]
        public void Replace_PhraseInsideAnotherWord_DoesNotReplaceIt()
        {
            var keywords = CreateKeywords(("agent", "AGENT"));

            var result = _service.Replace("agent agentowy superagent agent.", keywords);

            Assert.Equal("AGENT agentowy superagent AGENT.", result.Text);
        }

        [Fact]
        public void Replace_OverlappingPhrases_PrefersLongestPhrase()
        {
            var keywords = CreateKeywords(
                ("plik", "FILE"),
                ("plik agent", "AGENTS.md"));

            var result = _service.Replace("plik agent i plik", keywords);

            Assert.Equal("AGENTS.md i FILE", result.Text);
        }

        [Fact]
        public void Replace_ReplacementMatchesAnotherRule_DoesNotChainRules()
        {
            var keywords = CreateKeywords(
                ("plik agent", "AGENTS.md"),
                ("AGENTS.md", "README.md"));

            var result = _service.Replace("plik agent", keywords);

            Assert.Equal("AGENTS.md", result.Text);
        }

        [Fact]
        public void Replace_TextWithoutMatchingPhrase_ReturnsUnchangedText()
        {
            var keywords = CreateKeywords(("plik agent", "AGENTS.md"));
            const string text = "Otwórz plik ustawień.";

            var result = _service.Replace(text, keywords);

            Assert.Equal(text, result.Text);
            Assert.Equal(0, result.ReplacementCount);
        }

        [Fact]
        public void Replace_RegexCharactersInPhrase_TreatsThemLiterally()
        {
            var keywords = CreateKeywords(("C++", "cpp"), (".env", "environment"));

            var result = _service.Replace("C++ oraz .env", keywords);

            Assert.Equal("cpp oraz environment", result.Text);
        }

        [Fact]
        public void Replace_UnicodePhraseInsideAnotherWord_DoesNotReplaceIt()
        {
            var keywords = CreateKeywords(("żółć", "ZOLC"));

            var result = _service.Replace("żółć i zażółć", keywords);

            Assert.Equal("ZOLC i zażółć", result.Text);
        }

        [Fact]
        public void Replace_InvalidAndDuplicateSettings_IgnoresInvalidAndUsesFirstDuplicate()
        {
            var keywords = new List<DGV_KeyWords>
            {
                new() { Key = "  plik agent  ", Word = "  AGENTS.md  " },
                new() { Key = "PLIK AGENT", Word = "README.md" },
                new() { Key = "", Word = "pominięte" },
                new() { Key = "niepełne", Word = "" },
            };

            var result = _service.Replace("plik agent i niepełne", keywords);

            Assert.Equal("AGENTS.md i niepełne", result.Text);
        }

        [Fact]
        public void Replace_ReplacementWithIdenticalText_StillReportsMatch()
        {
            var keywords = CreateKeywords(("AGENTS.md", "AGENTS.md"));

            var result = _service.Replace("Otwórz AGENTS.md.", keywords);

            Assert.Equal("Otwórz AGENTS.md.", result.Text);
            Assert.Equal(1, result.ReplacementCount);
        }

        private static List<DGV_KeyWords> CreateKeywords(params (string Key, string Word)[] entries) =>
            entries.Select(entry => new DGV_KeyWords { Key = entry.Key, Word = entry.Word }).ToList();
    }
}
