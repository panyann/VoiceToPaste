using NAudio.Wave;
using Serilog;
using VoiceToPaste.Resources;

namespace VoiceToPaste
{
    /// <summary>
    /// Przechwytuje dźwięk z domyślnego mikrofonu systemowego jako PCM 16 kHz mono —
    /// natywny format wejściowy Whispera, więc próbki nie wymagają późniejszego resamplingu.
    /// Ostatnie nagranie trzymane jest wyłącznie w pamięci i nigdy nie trafia na dysk.
    /// </summary>
    public sealed class AudioRecorder : IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<AudioRecorder>();
        private static readonly WaveFormat CaptureFormat = new(16000, 16, 1);

        private WaveInEvent? _waveIn;
        private MemoryStream? _pcmBuffer;
        private TaskCompletionSource<float[]>? _stopCompletion;

        private WaveOutEvent? _waveOut;
        private RawSourceWaveStream? _playbackStream;

        private byte[] _lastRecording = [];

        public bool IsRecording => _waveIn != null;

        /// <summary>True, gdy system widzi co najmniej jedno urządzenie nagrywające.</summary>
        public static bool IsMicrophoneAvailable => WaveInEvent.DeviceCount > 0;

        /// <summary>Szczytowa amplituda ostatniego nagrania (0..1). Peak ≈ 0 oznacza ciszę.</summary>
        public float LastRecordingPeak { get; private set; }

        public TimeSpan LastRecordingDuration { get; private set; }

        public bool HasRecording => _lastRecording.Length > 0;

        /// <summary>
        /// Rozpoczyna nagrywanie z domyślnego mikrofonu.
        /// Rzuca InvalidOperationException przy braku mikrofonu lub gdy nagrywanie już trwa,
        /// a MmException, gdy urządzenie jest zajęte przez inną aplikację.
        /// </summary>
        public Task StartRecordingAsync(CancellationToken cancellationToken = default)
        {
            Logger.Information("Starting recording from the default microphone.");
            if (IsRecording)
            {
                Logger.Warning("Attempted to start recording while a recording is already in progress.");
                throw LocalizedExceptionFactory.InvalidOperation("RecordingAlreadyInProgress");
            }
            if (!IsMicrophoneAvailable)
            {
                Logger.Warning("No microphone was detected.");
                throw LocalizedExceptionFactory.InvalidOperation("MicrophoneNotFound");
            }

            // Zatrzymujemy odsłuch, żeby nie mieszać go z nowym nagrywaniem.
            StopPlayback();

            _pcmBuffer = new MemoryStream();
            _waveIn = new WaveInEvent { WaveFormat = CaptureFormat, BufferMilliseconds = 50 };
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            try
            {
                _waveIn.StartRecording();
                Logger.Information("Recording started.");
            }
            catch (Exception ex)
            {
                // StartRecording rzuca MmException, gdy urządzenie jest zajęte —
                // sprzątamy po sobie, żeby recorder wrócił do stanu Idle.
                ReleaseWaveIn();
                _pcmBuffer?.Dispose();
                _pcmBuffer = null;
                Logger.Error(ex, "Failed to start recording.");
                throw;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Zatrzymuje nagrywanie i zwraca próbki float gotowe dla Whispera.
        /// Oczekuje na zdarzenie RecordingStopped — NAudio dostarcza ostatni bufor
        /// dopiero w tym zdarzeniu, więc bez oczekiwania ucięlibyśmy końcówkę nagrania.
        /// </summary>
        public Task<float[]> StopRecordingAsync()
        {
            if (_waveIn == null)
            {
                Logger.Information("Skipped stopping the recording because no recording is active.");
                return Task.FromResult(Array.Empty<float>());
            }

            Logger.Information("Stopping recording.");
            _stopCompletion = new TaskCompletionSource<float[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waveIn.StopRecording();
            return _stopCompletion.Task;
        }

        /// <summary>Odtwarza ostatnie nagranie z pamięci przez domyślne urządzenie wyjściowe.</summary>
        public void PlayLastRecording()
        {
            if (!HasRecording)
            {
                Logger.Information("Skipped playback because there is no recording in memory.");
                return;
            }

            Logger.Information("Starting playback of the last recording.");
            StopPlayback();

            _playbackStream = new RawSourceWaveStream(new MemoryStream(_lastRecording, writable: false), CaptureFormat);
            _waveOut = new WaveOutEvent();
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            _waveOut.Init(_playbackStream);
            _waveOut.Play();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            _pcmBuffer?.Write(e.Buffer, 0, e.BytesRecorded);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            ReleaseWaveIn();

            if (e.Exception != null)
            {
                Logger.Error(e.Exception, "Recording stopped with an error.");
                _pcmBuffer?.Dispose();
                _pcmBuffer = null;
                _stopCompletion?.TrySetException(e.Exception);
                return;
            }

            _lastRecording = _pcmBuffer?.ToArray() ?? [];
            _pcmBuffer?.Dispose();
            _pcmBuffer = null;

            var samples = ConvertToFloat(_lastRecording);
            LastRecordingDuration = TimeSpan.FromSeconds((double)samples.Length / CaptureFormat.SampleRate);
            Logger.Information("Recording completed. Duration: {DurationSeconds:F1} s, signal detected: {HasSignal}.",
                LastRecordingDuration.TotalSeconds,
                LastRecordingPeak >= 0.001f);
            _stopCompletion?.TrySetResult(samples);
        }

        /// <summary>Konwertuje PCM16 na float i w tej samej pętli liczy peak nagrania.</summary>
        private float[] ConvertToFloat(byte[] pcm)
        {
            var samples = new float[pcm.Length / 2];
            var peak = 0f;

            for (var i = 0; i < samples.Length; i++)
            {
                var sample = BitConverter.ToInt16(pcm, i * 2) / 32768f;
                samples[i] = sample;

                var abs = Math.Abs(sample);
                if (abs > peak)
                    peak = abs;
            }

            LastRecordingPeak = peak;
            return samples;
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
                Logger.Error(e.Exception, "Recording playback stopped with an error.");
            else
                Logger.Information("Recording playback completed.");

            StopPlayback();
        }

        private void StopPlayback()
        {
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= OnPlaybackStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }

            _playbackStream?.Dispose();
            _playbackStream = null;
        }

        private void ReleaseWaveIn()
        {
            if (_waveIn == null)
                return;

            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        public void Dispose()
        {
            Logger.Information("Releasing audio resources.");
            if (_waveIn != null)
            {
                // Najpierw odpinamy zdarzenia — inaczej StopRecording wyzwoliłby
                // RecordingStopped na usuwanym obiekcie.
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.StopRecording();
                ReleaseWaveIn();
            }

            StopPlayback();
            _pcmBuffer?.Dispose();
            _pcmBuffer = null;
        }
    }
}
