using Serilog;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Services
{
    /// <summary>
    /// Koordynuje pojedynczą sesję dyktowania: nagrywanie, limit czasu, transkrypcję i wynik.
    /// Wszystkie żądania aktywacji są serializowane, więc timeout i skrót nie zatrzymają
    /// tego samego nagrania równocześnie.
    /// </summary>
    internal sealed class DictationController : IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<DictationController>();

        private readonly AudioRecorder _recorder = new();
        private readonly TranscriptionService _transcriptionService;
        private readonly TranscriptionOutput _output = new();
        private readonly KeywordReplacementService _keywordReplacementService = new();
        private readonly AppSettings _settings;
        private readonly DictationStateMachine _stateMachine = new();
        private readonly SemaphoreSlim _operationLock = new(1, 1);
        private readonly CancellationTokenSource _shutdownCancellation = new();
        private CancellationTokenSource? _recordingTimeoutCancellation;
        private long _recordingLimitTicks;
        private int _activationSuspensionCount;
        private bool _isDisposed;

        public DictationController(
            TranscriptionService transcriptionService,
            int recordingLimitSeconds,
            AppSettings settings)
        {
            _transcriptionService = transcriptionService;
            _settings = settings;
            SetRecordingLimit(recordingLimitSeconds);
        }

        public event Action<DictationState>? StateChanged;
        public event Action<string>? ErrorOccurred;

        public DictationState State => _stateMachine.State;

        /// <summary>
        /// Ustawia limit dla kolejnych nagrań. Aktywne oczekiwanie timeoutu korzysta z kopii
        /// utworzonej przy starcie nagrania, więc zmiana nie przerywa bieżącej sesji.
        /// </summary>
        public void SetRecordingLimit(int recordingLimitSeconds)
        {
            if (recordingLimitSeconds <= 0)
                throw LocalizedExceptionFactory.ArgumentOutOfRange(
                    "RecordingLimitMustBePositive",
                    nameof(recordingLimitSeconds));

            Interlocked.Exchange(ref _recordingLimitTicks, TimeSpan.FromSeconds(recordingLimitSeconds).Ticks);
        }

        /// <summary>Wstrzymuje aktywacje podczas używania testera z osobnym rejestratorem.</summary>
        public void SetActivationsSuspended(bool suspended)
        {
            _activationSuspensionCount = Math.Max(0, _activationSuspensionCount + (suspended ? 1 : -1));
        }

        public Task HandleActivationAsync() => HandleActivationCoreAsync(isRecordingLimitReached: false);

        private async Task HandleActivationCoreAsync(bool isRecordingLimitReached)
        {
            if ((_activationSuspensionCount > 0 && !isRecordingLimitReached) || _isDisposed)
                return;

            Task? transcriptionTask = null;
            var operationLockAcquired = false;

            try
            {
                await _operationLock.WaitAsync(_shutdownCancellation.Token);
                operationLockAcquired = true;

                if ((_activationSuspensionCount > 0 && !isRecordingLimitReached) || _isDisposed || State == DictationState.Transcribing)
                    return;

                if (State == DictationState.Idle)
                {
                    await StartRecordingAsync();
                    return;
                }

                var samples = await StopRecordingAsync();
                if (samples.Length == 0 || _recorder.LastRecordingPeak < 0.001f)
                {
                    Logger.Information("Skipped transcription of an empty or silent recording.");
                    ReturnToIdle();
                    return;
                }

                ChangeToTranscribing();
                transcriptionTask = TranscribeAndOutputAsync(samples);
            }
            catch (OperationCanceledException) when (_shutdownCancellation.IsCancellationRequested)
            {
                ReturnToIdle();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to handle dictation activation.");
                ReportError(UiStrings.Format("DictationError", LocalizedExceptionFactory.GetUserMessage(ex)));
                ReturnToIdle();
            }
            finally
            {
                if (operationLockAcquired)
                    _operationLock.Release();
            }

            if (transcriptionTask != null)
                await transcriptionTask;
        }

        private async Task StartRecordingAsync()
        {
            await _recorder.StartRecordingAsync(_shutdownCancellation.Token);
            if (!_stateMachine.TryStartRecording())
                throw LocalizedExceptionFactory.InvalidOperation("RecordingUnavailableInCurrentState");

            NotifyStateChanged();
            StartRecordingTimeout(GetRecordingLimit());
        }

        private async Task<float[]> StopRecordingAsync()
        {
            CancelRecordingTimeout();
            return await _recorder.StopRecordingAsync();
        }

        private async Task TranscribeAndOutputAsync(float[] samples)
        {
            try
            {
                string text;
                try
                {
                    text = await _transcriptionService.TranscribeAsync(samples, _shutdownCancellation.Token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !_shutdownCancellation.IsCancellationRequested)
                {
                    Logger.Error(ex, "Transcription failed.");
                    ReportError(UiStrings.Format("TranscriptionError", LocalizedExceptionFactory.GetUserMessage(ex)));
                    return;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    Logger.Information("Whisper did not recognize speech in the recording.");
                    return;
                }

                text = ApplyKeywordReplacements(text);

                try
                {
                    await _output.WriteAndPasteAsync(text, _shutdownCancellation.Token);
                }
                catch (PasteFailedException ex)
                {
                    Logger.Error(ex, "Failed to paste the transcription into the active window.");
                    ReportError(UiStrings.Get("TranscriptionPasteFailedClipboardRetained"));
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !_shutdownCancellation.IsCancellationRequested)
                {
                    Logger.Error(ex, "Failed to deliver the transcription result.");
                    ReportError(UiStrings.Format(
                        "TranscriptionClipboardWriteFailed",
                        LocalizedExceptionFactory.GetUserMessage(ex)));
                }
            }
            catch (OperationCanceledException) when (_shutdownCancellation.IsCancellationRequested)
            {
                Logger.Information("Dictation was canceled while the application was shutting down.");
            }
            finally
            {
                ReturnToIdle();
            }
        }

        /// <summary>
        /// Stosuje aktualne reguły użytkownika do kompletnego tekstu transkrypcji.
        /// To wspólny punkt dla przyszłego rozszerzania korekty rozpoznanego tekstu.
        /// </summary>
        private string ApplyKeywordReplacements(string text) =>
            _keywordReplacementService.Replace(text, _settings.KeyWords).Text;

        private void StartRecordingTimeout(TimeSpan recordingLimit)
        {
            _recordingTimeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCancellation.Token);
            _ = StopAfterRecordingLimitAsync(recordingLimit, _recordingTimeoutCancellation.Token);
        }

        private async Task StopAfterRecordingLimitAsync(TimeSpan recordingLimit, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(recordingLimit, cancellationToken);
                Logger.Information("Reached the recording limit of {RecordingLimitSeconds} seconds.", recordingLimit.TotalSeconds);
                await HandleActivationCoreAsync(isRecordingLimitReached: true);
            }
            catch (OperationCanceledException)
            {
                // Ręczne zatrzymanie lub zamykanie aplikacji anuluje oczekiwanie timeoutu.
            }
        }

        private TimeSpan GetRecordingLimit() =>
            TimeSpan.FromTicks(Interlocked.Read(ref _recordingLimitTicks));

        private void ChangeToTranscribing()
        {
            if (!_stateMachine.TryStartTranscribing())
                throw LocalizedExceptionFactory.InvalidOperation("TranscriptionUnavailableInCurrentState");

            NotifyStateChanged();
        }

        private void ReturnToIdle()
        {
            CancelRecordingTimeout();
            if (_stateMachine.TryReturnToIdle())
                NotifyStateChanged();
        }

        private void CancelRecordingTimeout()
        {
            _recordingTimeoutCancellation?.Cancel();
            _recordingTimeoutCancellation?.Dispose();
            _recordingTimeoutCancellation = null;
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke(State);
        }

        private void ReportError(string message)
        {
            ErrorOccurred?.Invoke(message);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _shutdownCancellation.Cancel();
            CancelRecordingTimeout();
            _recorder.Dispose();
            _shutdownCancellation.Dispose();
            _operationLock.Dispose();
        }
    }
}
