using Whisper.net.LibraryLoader;
using Serilog;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;

namespace VoiceToPaste.Services
{
    /// <summary>Jawny stan cyklu życia singletona <see cref="TranscriptionService"/>.</summary>
    public enum TranscriptionServiceState
    {
        /// <summary>Inicjalizacja jeszcze nie ruszyła.</summary>
        NotStarted,
        Loading,
        Ready,
        Failed,
        Disposed
    }

    /// <summary>
    /// Jeden współdzielony punkt dostępu do transkrypcji w całej aplikacji.
    /// Właściciel jedynej instancji WhisperTranscribera — model (~3 GB) ładuje się raz
    /// i zostaje w pamięci do końca procesu. Kod aplikacji używa TranscriptionService.Instance.
    /// </summary>
    public sealed class TranscriptionService : IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<TranscriptionService>();
        private readonly object _stateLock = new();
        private readonly string? _modelPath;

        private TranscriptionServiceState _state = TranscriptionServiceState.NotStarted;
        private TranscriptionBackend _backend;
        private string _language = TranscriptionLanguages.GetSystemDefaultCode();
        private WhisperTranscriber? _transcriber;
        private Task? _initializationTask;
        private Exception? _initializationError;

        // Singleton: ścieżka do modelu rozwiązywana jest leniwie przy inicjalizacji,
        // żeby brak modelu nie wysypał aplikacji przy pierwszym dotknięciu Instance.
        private TranscriptionService()
        {
        }

        // Testy podają własną ścieżkę modelu, żeby nie ładować prawdziwych ~3 GB.
        internal TranscriptionService(string modelPath)
        {
            _modelPath = modelPath;
        }

        public static TranscriptionService Instance { get; } = new();

        public TranscriptionServiceState State
        {
            get { lock (_stateLock) return _state; }
        }

        /// <summary>Błąd inicjalizacji modelu; null, gdy ładowanie się powiodło lub jeszcze nie ruszyło.</summary>
        public Exception? InitializationError
        {
            get { lock (_stateLock) return _initializationError; }
        }

        /// <summary>Biblioteka natywna faktycznie załadowana; null, zanim model się załaduje.</summary>
        public RuntimeLibrary? LoadedRuntime
        {
            get { lock (_stateLock) return _transcriber?.LoadedRuntime; }
        }

        /// <summary>Silnik wybrany przy rozpoczęciu inicjalizacji; null, gdy jeszcze nie ruszyła.</summary>
        public TranscriptionBackend? SelectedBackend
        {
            get { lock (_stateLock) return _initializationTask == null ? null : _backend; }
        }

        /// <summary>
        /// Aktualny kod języka transkrypcji. Wartość może zostać zmieniona bez ponownego
        /// ładowania modelu i obowiązuje przy następnym nagraniu.
        /// </summary>
        public string Language
        {
            get { lock (_stateLock) return _transcriber?.Language ?? _language; }
        }

        /// <summary>
        /// Ustawia język dla kolejnych transkrypcji. Jeśli model już powstaje albo jest gotowy,
        /// przekazuje wartość do jego jedynego transcribera; w przeciwnym razie zapamiętuje ją
        /// na czas przyszłej inicjalizacji.
        /// </summary>
        public void SetLanguage(string language)
        {
            var normalizedLanguage = TranscriptionLanguages.Normalize(language);

            lock (_stateLock)
            {
                ThrowIfDisposed();
                _language = normalizedLanguage;
                if (_transcriber != null)
                    _transcriber.Language = normalizedLanguage;
            }

            Logger.Information("Set transcription language to {Language}.", normalizedLanguage);
        }

        /// <summary>
        /// Rozpoczyna ładowanie wskazanego modelu dla wybranego silnika. Wielokrotne wywołania
        /// z tym samym silnikiem zwracają to samo zadanie — model ładuje się dokładnie raz.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Próba inicjalizacji z innym silnikiem po rozpoczęciu ładowania. Natywny runtime
        /// lockuje się raz na proces, więc zmiana silnika wymaga restartu aplikacji.
        /// </exception>
        public Task StartInitializationAsync(
            TranscriptionBackend backend,
            WhisperModel model,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            lock (_stateLock)
            {
                ThrowIfDisposed();

                if (_initializationTask != null)
                {
                    if (_backend == backend)
                    {
                        Logger.Information("Model initialization for backend {Backend} is already in progress or completed.", backend);
                        return _initializationTask;
                    }

                    Logger.Warning("Attempted to change backend from {CurrentBackend} to {RequestedBackend} without restarting.", _backend, backend);
                    throw LocalizedExceptionFactory.InvalidOperation(
                        "TranscriptionBackendAlreadyInitialized",
                        _backend,
                        backend);
                }

                _backend = backend;
                _state = TranscriptionServiceState.Loading;
                Logger.Information("Starting Whisper model preload for backend {Backend}.", backend);
                // Async metoda nigdy nie rzuci synchronicznie — błąd (nawet brak pliku modelu)
                // trafi do zwróconego zadania, a stan zmieni się na Failed.
                _initializationTask = InitializeCoreAsync(backend, model, cancellationToken);
                return _initializationTask;
            }
        }

        /// <summary>
        /// Transkrybuje próbki PCM float (16 kHz mono). Jeśli preload jeszcze trwa, czeka na niego;
        /// błąd inicjalizacji propaguje do wywołującego. Równoległe wywołania serializuje semafor
        /// wewnątrz WhisperTranscribera — ciężki inference nigdy nie liczy się równocześnie.
        /// </summary>
        public async Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Logger.Information("Starting transcription.");
            Task initializationTask;
            lock (_stateLock)
            {
                ThrowIfDisposed();

                if (_initializationTask == null)
                    throw LocalizedExceptionFactory.InvalidOperation("TranscriptionInitializationRequired");

                initializationTask = _initializationTask;
            }

            await initializationTask;

            WhisperTranscriber transcriber;
            lock (_stateLock)
            {
                ThrowIfDisposed();
                // Po pomyślnej inicjalizacji transkryber na pewno już jest przypisany.
                transcriber = _transcriber!;
            }

            try
            {
                var text = await transcriber.TranscribeAsync(samples, cancellationToken);
                stopwatch.Stop();
                Logger.Information("Transcription completed in {ElapsedMilliseconds} ms. Text recognized: {HasText}.",
                    stopwatch.ElapsedMilliseconds,
                    !string.IsNullOrWhiteSpace(text));
                return text;
            }
            catch (OperationCanceledException)
            {
                Logger.Warning("Transcription was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Transcription failed.");
                throw;
            }
        }

        private async Task InitializeCoreAsync(
            TranscriptionBackend backend,
            WhisperModel model,
            CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var transcriber = new WhisperTranscriber(_modelPath ?? WhisperTranscriber.ResolveModelPath(model), backend);

                lock (_stateLock)
                {
                    if (_state == TranscriptionServiceState.Disposed)
                    {
                        transcriber.Dispose();
                        throw new ObjectDisposedException(nameof(TranscriptionService));
                    }

                    // Język mógł zmienić się między utworzeniem transcribera a wejściem do locka.
                    transcriber.Language = _language;
                    _transcriber = transcriber;
                }

                await transcriber.InitializeAsync(cancellationToken);

                lock (_stateLock)
                {
                    // Zamknięcie aplikacji w trakcie ładowania wygrywa — stanu Disposed nie nadpisujemy.
                    if (_state == TranscriptionServiceState.Loading)
                        _state = TranscriptionServiceState.Ready;
                }

                stopwatch.Stop();
                Logger.Information(
                    "Whisper model is ready. Backend: {Backend}, runtime: {Runtime}, load time: {ElapsedMilliseconds} ms.",
                    backend,
                    transcriber.LoadedRuntime,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                Logger.Warning("Whisper model preload was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    if (_state == TranscriptionServiceState.Loading)
                    {
                        _state = TranscriptionServiceState.Failed;
                        _initializationError = ex;
                    }
                }
                Logger.Error(ex, "Whisper model preload failed for backend {Backend}.", backend);
                throw;
            }
        }

        /// <summary>Zwalnia model dokładnie raz — przy zamykaniu procesu.</summary>
        public void Dispose()
        {
            Logger.Information("Disposing the shared Whisper model.");
            WhisperTranscriber? transcriber;
            lock (_stateLock)
            {
                if (_state == TranscriptionServiceState.Disposed)
                    return;

                _state = TranscriptionServiceState.Disposed;
                transcriber = _transcriber;
            }

            // Poza lockiem — zwolnienie może czekać na dokończenie ładowania lub transkrypcji.
            transcriber?.Dispose();
            Logger.Information("The shared Whisper model was disposed.");
        }

        // Wywoływać wyłącznie pod _stateLock.
        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_state == TranscriptionServiceState.Disposed, this);
        }
    }
}
