namespace VoiceToPaste.Models
{
    /// <summary>
    /// Ustawienia aplikacji zapisywane w pliku settings.yaml obok pliku wykonywalnego.
    /// </summary>
    public sealed class AppSettings
    {
        public const int DefaultRecordingLimitSeconds = 60;
        public const int MinimumRecordingLimitSeconds = 1;
        public const int MaximumRecordingLimitSeconds = 3600;

        /// <summary>
        /// Silnik transkrypcji. Zmiana zaczyna obowiązywać po restarcie aplikacji,
        /// bo Whisper.net ładuje natywny runtime tylko raz na proces.
        /// </summary>
        public TranscriptionBackend TranscriptionEngine { get; set; } = TranscriptionBackend.Auto;

        /// <summary>
        /// Identyfikator lokalnego modelu Whisper. Zmiana zaczyna obowiązywać po restarcie,
        /// ponieważ załadowanego modelu nie można bezpiecznie podmienić w tym samym procesie.
        /// </summary>
        public string? WhisperModelId { get; set; }

        /// <summary>
        /// Kod języka transkrypcji. Przy pierwszym uruchomieniu wybierany jest język
        /// interfejsu Windows, a nieobsługiwany język włącza automatyczne wykrywanie.
        /// </summary>
        public string TranscribeLanguage { get; set; } = TranscriptionLanguages.GetSystemDefaultCode();

        /// <summary>
        /// Kod języka interfejsu. Wartość jest niezależna od języka transkrypcji Whispera.
        /// </summary>
        public string UiLanguage { get; set; } = UiLanguages.GetSystemDefaultCode();

        /// <summary>
        /// Określa, czy okno ustawień ma pozostać ukryte po uruchomieniu aplikacji.
        /// </summary>
        public bool StartInTray { get; set; }

        /// <summary>
        /// Określa, czy aplikacja ma być uruchamiana przez Harmonogram zadań po zalogowaniu użytkownika.
        /// </summary>
        public bool AutoStart { get; set; }

        /// <summary>
        /// Maksymalny czas pojedynczego nagrania w sekundach.
        /// </summary>
        public int RecordingLimitSeconds { get; set; } = DefaultRecordingLimitSeconds;

        /// <summary>
        /// Globalny skrót aktywujący dyktowanie. Wartość null oznacza wyłączenie skrótu.
        /// </summary>
        public HotkeyGesture? Hotkey { get; set; } = HotkeyGesture.CreateDefault();

        /// <summary>
        /// Reguły podmiany fraz w transkrypcji: Key to rozpoznana fraza, Word to tekst docelowy.
        /// </summary>
        public List<DGV_KeyWords> KeyWords { get; set; } = [];
    }
}
