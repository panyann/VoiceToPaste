using System.Runtime.ExceptionServices;
using System.Text;
using Serilog;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace VoiceToPaste
{
    /// <summary>Silnik obliczeniowy Whispera wybierany przez użytkownika.</summary>
    public enum TranscriptionBackend
    {
        /// <summary>Domyślna kolejność loadera: GPU (CUDA), a gdy niedostępne — CPU.</summary>
        Auto,
        Gpu,
        Cpu
    }

    /// <summary>
    /// Lokalna transkrypcja nagrań przez Whisper (whisper.cpp) — żadne dane nie opuszczają maszyny.
    /// Model (~3 GB) można załadować z wyprzedzeniem przez InitializeAsync albo leniwie
    /// przy pierwszej transkrypcji; trzymany jest w pamięci do Dispose.
    /// Klasa nie zna UI — stan diagnostyczny wystawia przez zwykłe właściwości.
    /// </summary>
    public sealed class WhisperTranscriber : IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext<WhisperTranscriber>();

        private readonly string _modelPath;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private string _language = TranscriptionLanguages.GetSystemDefaultCode();
        private WhisperFactory? _factory;
        private Exception? _initializationError;
        private bool _disposed;

        public WhisperTranscriber(string modelPath, TranscriptionBackend backend = TranscriptionBackend.Auto)
        {
            _modelPath = modelPath;
            Backend = backend;
        }

        /// <summary>Silnik wybrany przy tworzeniu transkrybera.</summary>
        public TranscriptionBackend Backend { get; }

        /// <summary>
        /// Kod języka Whispera albo <c>auto</c> dla automatycznego wykrywania.
        /// Zmiana działa przy następnej transkrypcji i nie wymaga przeładowania modelu.
        /// </summary>
        public string Language
        {
            get => Volatile.Read(ref _language);
            set => Volatile.Write(ref _language, TranscriptionLanguages.Normalize(value));
        }

        /// <summary>Biblioteka natywna faktycznie załadowana; null, zanim model się załaduje.</summary>
        public RuntimeLibrary? LoadedRuntime { get; private set; }

        /// <summary>Model załadowany i gotowy do transkrypcji.</summary>
        public bool IsReady => _factory != null;

        /// <summary>Błąd inicjalizacji modelu; null, gdy ładowanie się powiodło lub jeszcze nie ruszyło.</summary>
        public Exception? InitializationError => _initializationError;

        /// <summary>Buduje ścieżkę modelu wyłącznie w katalogu LLM bezpośrednio obok EXE.</summary>
        public static string GetModelPath(WhisperModel model)
        {
            ArgumentNullException.ThrowIfNull(model);
            return Path.Combine(ApplicationPaths.Directory, "LLM", model.FileName);
        }

        /// <summary>Zweryfikowuje obecność wybranego modelu w katalogu LLM obok EXE.</summary>
        public static string ResolveModelPath(WhisperModel model)
        {
            var candidate = GetModelPath(model);
            if (File.Exists(candidate))
            {
                Logger.Information("Found Whisper model file {ModelId}.", model.Id);
                return candidate;
            }

            var exception = LocalizedExceptionFactory.FileNotFound(
                "WhisperModelNotFound",
                model.DisplayName,
                Path.GetDirectoryName(candidate));
            Logger.Error(exception, "Whisper model file {ModelId} was not found.", model.Id);
            throw exception;
        }

        /// <summary>
        /// Ładuje model i natywny runtime bez wykonywania transkrypcji. Wielokrotne i równoległe
        /// wywołania współdzielą jedno ładowanie. Po pierwszym niepowodzeniu kolejne wywołania
        /// rzucają zapisany błąd, zamiast ponownie próbować mapować ~3 GB.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_factory != null)
            {
                Logger.Information("The Whisper model is already loaded.");
                return;
            }

            Logger.Information("Starting transcriber initialization for backend {Backend}.", Backend);
            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                ThrowIfDisposed();
                await LoadCoreAsync(cancellationToken);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>
        /// Transkrybuje próbki PCM float (16 kHz mono) do tekstu.
        /// Jeśli ładowanie modelu właśnie trwa, po prostu na niego czeka.
        /// </summary>
        public async Task<string> TranscribeAsync(float[] samples, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (samples.Length == 0)
                return string.Empty;

            // Semafor obejmuje też inference — dzięki temu Dispose nigdy nie zwolni fabryki
            // w połowie transkrypcji, a równoległe wywołania ustawią się w kolejce zamiast
            // równocześnie liczyć ciężki inference.
            await _loadLock.WaitAsync(cancellationToken);
            try
            {
                ThrowIfDisposed();
                var factory = await LoadCoreAsync(cancellationToken);
                // Zmiana ComboBoxa w trakcie obliczeń może dotyczyć dopiero następnego nagrania.
                var language = Language;

                // Inference whisper.cpp jest synchroniczny względem enumeracji wyników,
                // więc spychamy go na wątek roboczy — UI zostaje responsywne.
                return await Task.Run(() => TranscribeCoreAsync(factory, samples, language, cancellationToken), cancellationToken);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        /// <summary>
        /// Właściwe ładowanie modelu — wywołujący MUSI trzymać _loadLock.
        /// FromPath mapuje ~3 GB do pamięci natywnej, więc robimy to na wątku roboczym.
        /// </summary>
        private async Task<WhisperFactory> LoadCoreAsync(CancellationToken cancellationToken)
        {
            if (_factory != null)
                return _factory;

            if (_initializationError != null)
                // Rzucony przez Capture oryginalny wyjątek zachowuje swój stack trace.
                ExceptionDispatchInfo.Capture(_initializationError).Throw();

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                ApplyBackendPreference();
                _factory = await Task.Run(() =>
                {
                    var created = WhisperFactory.FromPath(_modelPath);
                    try
                    {
                        // FromPath nie zgłasza błędu brakującego/uszkodzonego modelu — kontekst
                        // natywny jest wtedy IntPtr.Zero, a WhisperModelLoadException leci dopiero
                        // z CreateBuilder. Wywołujemy go od razu, żeby inicjalizacja faktycznie
                        // potwierdziła, że model działa.
                        created.CreateBuilder();
                        return created;
                    }
                    catch
                    {
                        created.Dispose();
                        throw;
                    }
                }, cancellationToken);
                LoadedRuntime = RuntimeOptions.LoadedLibrary;
                stopwatch.Stop();
                Logger.Information("Loaded native Whisper runtime {Runtime} for backend {Backend} in {ElapsedMilliseconds} ms.",
                    LoadedRuntime,
                    Backend,
                    stopwatch.ElapsedMilliseconds);
                return _factory;
            }
            catch (OperationCanceledException)
            {
                // Anulowanie nie jest wadą modelu — następne wywołanie spróbuje ponownie.
                Logger.Warning("Native Whisper runtime initialization was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                _initializationError = ex;
                Logger.Error(ex, "Failed to load the Whisper model or runtime.");
                throw;
            }
        }

        /// <summary>
        /// Ustawia preferowaną kolejność bibliotek natywnych. Działa tylko przed utworzeniem
        /// pierwszej fabryki w procesie — potem loader ma już zalockowaną bibliotekę
        /// i zmiana silnika wymaga restartu aplikacji.
        /// </summary>
        private void ApplyBackendPreference()
        {
            switch (Backend)
            {
                case TranscriptionBackend.Gpu:
                    RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cuda, RuntimeLibrary.Cpu];
                    Logger.Information("Set runtime preference to CUDA, then CPU.");
                    break;
                case TranscriptionBackend.Cpu:
                    RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Cpu];
                    Logger.Information("Set runtime preference to CPU.");
                    break;
                    // Auto: zostawiamy domyślną kolejność loadera (CUDA → CPU).
            }
        }

        private static async Task<string> TranscribeCoreAsync(
            WhisperFactory factory, float[] samples, string language, CancellationToken cancellationToken)
        {
            // Domyślnie whisper.cpp ogranicza się do 4 wątków — przy large-v3 na CPU
            // oddanie wszystkich rdzeni skraca transkrypcję nawet kilkukrotnie.
            var builder = factory.CreateBuilder().WithThreads(Environment.ProcessorCount);
            if (language == TranscriptionLanguages.AutoDetectCode)
                builder.WithLanguageDetection();
            else
                builder.WithLanguage(language);

            using var processor = builder.Build();

            var text = new StringBuilder();

            await foreach (var segment in processor.ProcessAsync(samples, cancellationToken))
            {
                var segmentText = segment.Text.Trim();
                if (segmentText.Length == 0)
                    continue;

                if (text.Length > 0)
                    text.Append(' ');
                text.Append(segmentText);
            }

            return text.ToString();
        }

        /// <summary>
        /// Zwalnia ~3 GB natywnej pamięci modelu. Jeśli ładowanie lub transkrypcja właśnie
        /// trwają, czeka na ich koniec — FromPath to synchroniczne wywołanie natywne,
        /// którego nie da się anulować, a fabryka nie może uciec po zwolnieniu semafora.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _loadLock.Wait();
            try
            {
                if (_disposed)
                    return;

                _disposed = true;
                _factory?.Dispose();
                _factory = null;
                Logger.Information("Released the native Whisper factory.");
            }
            finally
            {
                _loadLock.Release();
                _loadLock.Dispose();
            }
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
