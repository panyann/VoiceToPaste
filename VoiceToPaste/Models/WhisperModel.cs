using System.Globalization;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Models
{
    /// <summary>Opis jednego pliku modelu Whisper dostępnego do pobrania.</summary>
    public sealed record WhisperModel(
        string Id,
        string DisplayName,
        string DescriptionKey,
        string FileName,
        long SizeBytes,
        Uri DownloadUri,
        string Sha256)
    {
        public string Description => GetDescription(CultureInfo.CurrentUICulture);

        /// <summary>Zwraca opis modelu w kulturze wskazanej przez interfejs lub test.</summary>
        internal string GetDescription(CultureInfo culture)
        {
            return UiStrings.Get(DescriptionKey, culture);
        }
    }
}
