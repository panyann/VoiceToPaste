using Serilog;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;
using VoiceToPaste.Services;
using Whisper.net.LibraryLoader;

namespace VoiceToPaste
{
    public partial class TesterForm : Form
    {
        private static readonly ILogger Logger = Serilog.Log.ForContext<TesterForm>();
        private readonly AudioRecorder _recorder = new();
        private readonly KeywordReplacementService _keywordReplacementService = new();
        private readonly AppSettings _settings;

        public TesterForm(AppSettings settings)
        {
            _settings = settings;
            InitializeComponent();
            Logger.Information("The transcription tester was opened.");
            LogTranscriptionServiceStatus();
        }

        /// <summary>
        /// Wypisuje aktualny stan współdzielonego modelu. Zwraca false, gdy transkrypcja
        /// nie może zostać uruchomiona bez zmiany stanu aplikacji.
        /// </summary>
        private bool LogTranscriptionServiceStatus()
        {
            var transcriptionService = TranscriptionService.Instance;

            switch (transcriptionService.State)
            {
                case TranscriptionServiceState.Loading:
                    Log(UiStrings.Get("TesterModelLoading"));
                    return true;
                case TranscriptionServiceState.Ready:
                    LogLoadedRuntime(transcriptionService.SelectedBackend, transcriptionService.LoadedRuntime);
                    return true;
                case TranscriptionServiceState.Failed:
                    var initializationError = transcriptionService.InitializationError is Exception exception
                        ? LocalizedExceptionFactory.GetUserMessage(exception)
                        : UiStrings.Get("TesterUnknownError");

                    Log(UiStrings.Format("TesterModelLoadFailed", initializationError));
                    return false;
                case TranscriptionServiceState.NotStarted:
                    Log(UiStrings.Get("TesterModelNotInitialized"));
                    return false;
                case TranscriptionServiceState.Disposed:
                    Log(UiStrings.Get("TesterModelDisposed"));
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Wyjaśnia wynik wyboru backendu na podstawie runtime'u załadowanego przez Whisper.net.
        /// Dzięki temu użytkownik odróżnia poprawny wybór CUDA od kontrolowanego fallbacku na CPU.
        /// </summary>
        private void LogLoadedRuntime(TranscriptionBackend? selectedBackend, RuntimeLibrary? loadedRuntime)
        {
            var runtime = loadedRuntime?.ToString();
            if (runtime is null)
                runtime = UiStrings.Get("TesterUnknownRuntime");

            if (selectedBackend == TranscriptionBackend.Auto && loadedRuntime == RuntimeLibrary.Cpu)
            {
                Log(UiStrings.Format("TesterRuntimeAutoCpu", runtime));
                return;
            }

            if (selectedBackend == TranscriptionBackend.Auto && loadedRuntime == RuntimeLibrary.Cuda)
            {
                Log(UiStrings.Format("TesterRuntimeAutoCuda", runtime));
                return;
            }

            if (selectedBackend == TranscriptionBackend.Gpu && loadedRuntime != RuntimeLibrary.Cuda)
            {
                Log(UiStrings.Format("TesterRuntimeGpuMismatch", runtime));
                return;
            }

            if (selectedBackend == TranscriptionBackend.Cpu && loadedRuntime != RuntimeLibrary.Cpu)
            {
                Log(UiStrings.Format("TesterRuntimeCpuMismatch", runtime));
                return;
            }

            Log(UiStrings.Format("TesterRuntimeReady", runtime));
        }

        private async void btnRecord_Click(object sender, EventArgs e)
        {
            if (!_recorder.IsRecording)
            {
                try
                {
                    await _recorder.StartRecordingAsync();
                    btnRecord.Text = UiStrings.Get("TesterStopRecordingButton");
                    Log(UiStrings.Get("TesterRecording"));
                }
                catch (Exception ex)
                {
                    // Łapiemy szeroko na granicy UI — błąd nie może ubić aplikacji działającej w tle.
                    Logger.Error(ex, "Failed to start recording in the transcription tester.");
                    Log(UiStrings.Format("TesterRecordingError", LocalizedExceptionFactory.GetUserMessage(ex)));
                }
            }
            else
            {
                // Blokada na czas zatrzymywania, żeby podwójny klik nie odpalił drugiego nagrania.
                btnRecord.Enabled = false;
                try
                {
                    var samples = await _recorder.StopRecordingAsync();
                    btnPlay.Enabled = _recorder.HasRecording;
                    LogRecordingSummary(samples.Length);
                    // Przycisk nadal zablokowany — drugie nagranie nie wystartuje w trakcie transkrypcji.
                    await TranscribeRecordingAsync(samples);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to stop recording in the transcription tester.");
                    Log(UiStrings.Format("TesterRecordingError", LocalizedExceptionFactory.GetUserMessage(ex)));
                }
                finally
                {
                    btnRecord.Text = UiStrings.Get("TesterRecordButton");
                    btnRecord.Enabled = true;
                }
            }
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            try
            {
                _recorder.PlayLastRecording();
                Log(UiStrings.Get("TesterPlayback"));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to play the recording in the transcription tester.");
                Log(UiStrings.Format("TesterPlaybackError", LocalizedExceptionFactory.GetUserMessage(ex)));
            }
        }

        /// <summary>
        /// Transkrybuje nagranie przez singleton. Przy trwającym preloadzie czeka na ten sam model,
        /// przy błędzie inicjalizacji pokazuje diagnostykę, a po sukcesie wypisuje surowy tekst
        /// i wynik korekty, jeśli dopasowano co najmniej jedną regułę słów kluczowych.
        /// </summary>
        private async Task TranscribeRecordingAsync(float[] samples)
        {
            if (samples.Length == 0 || _recorder.LastRecordingPeak < 0.001f)
            {
                Logger.Information("Skipped transcription of an empty or silent recording.");
                Log(UiStrings.Get("TesterSilentRecordingSkipped"));
                return;
            }

            if (!LogTranscriptionServiceStatus())
                return;

            Log(UiStrings.Format("TesterTranscribing", "large-v3", TranscriptionService.Instance.Language));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var text = await TranscriptionService.Instance.TranscribeAsync(samples);
                stopwatch.Stop();

                LogTranscriptionServiceStatus();
                if (text.Length == 0)
                {
                    Log(UiStrings.Format("TesterSpeechNotRecognized", stopwatch.Elapsed.TotalSeconds));
                    return;
                }

                Log(UiStrings.Format("TesterRawTranscription", stopwatch.Elapsed.TotalSeconds, text));
                var replacementResult = _keywordReplacementService.Replace(text, _settings.KeyWords);
                if (replacementResult.ReplacementCount > 0)
                {
                    Log(UiStrings.Format(
                        "TesterKeywordReplacementResult",
                        replacementResult.ReplacementCount,
                        replacementResult.Text));
                }
            }
            catch (Exception ex)
            {
                // Łapiemy szeroko na granicy UI — błąd Whispera nie może ubić aplikacji w tle.
                Logger.Error(ex, "Transcription in the transcription tester failed.");
                LogTranscriptionServiceStatus();
                Log(UiStrings.Format("TesterTranscriptionError", LocalizedExceptionFactory.GetUserMessage(ex)));
            }
        }

        private void LogRecordingSummary(int sampleCount)
        {
            var duration = _recorder.LastRecordingDuration;
            var peak = _recorder.LastRecordingPeak;
            var peakDb = double.NegativeInfinity;
            if (peak > 0)
                peakDb = 20 * Math.Log10(peak);

            var message = UiStrings.Format(
                "TesterRecordingSummary",
                duration.TotalSeconds,
                sampleCount,
                peakDb);
            if (peak < 0.001f)
                message += UiStrings.Get("TesterRecordingSilenceWarning");

            Log(message);
        }

        private void Log(string message)
        {
            richTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Logger.Information("The transcription tester is closing.");
            _recorder.Dispose();
            base.OnFormClosing(e);
        }
    }
}
