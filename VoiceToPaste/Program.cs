using Serilog;
using System.Diagnostics;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;
using VoiceToPaste.Services;

namespace VoiceToPaste
{
    internal static class Program
    {
        private const string SingleInstanceMutexName = "VoiceToPaste.SingleInstance";

        internal static string LogDirectory { get; } = Path.Combine(ApplicationPaths.Directory, "logs");

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ConfigureLogger();
            TranscriptionService? transcriptionService = null;
            var restartRequested = false;

            try
            {
                var settingsService = new SettingsService();
                var settings = settingsService.Load();

                // Kulturę ustawiamy przed inicjalizacją WinForms, aby zasoby formularzy,
                // komunikaty oraz zadania tworzone później używały tego samego języka.
                UiLanguages.ApplyCulture(settings.UiLanguage);

                using var singleInstanceMutex = new Mutex(
                    initiallyOwned: false,
                    name: SingleInstanceMutexName,
                    createdNew: out var isFirstInstance);

                if (!isFirstInstance)
                {
                    MessageBox.Show(
                        UiStrings.Get("ApplicationAlreadyRunning"),
                        UiStrings.Get("ApplicationTitle"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // Uchwyt muteksa pozostaje otwarty do końca procesu, aby druga instancja nie
                // rozpoczęła inicjalizacji usług ani nie utworzyła własnej ikony tray.
                Log.Information("=== Starting VoiceToPaste, version {ApplicationVersion}. ===",
                    GetApplicationVersion());
                ApplicationConfiguration.Initialize();
                Application.ThreadException += (_, eventArgs) =>
                    Log.Error(eventArgs.Exception, "Unhandled exception on the UI thread.");
                AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
                    Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled process exception.");

                var autoStartTaskService = new AutoStartTaskService();
                var selectedModel = WhisperModelCatalog.TryGetById(settings.WhisperModelId);
                var isSelectedModelAvailable = selectedModel != null && File.Exists(WhisperTranscriber.GetModelPath(selectedModel));
                var cudaRuntimeService = new CudaRuntimeService();
                var preloadBackend = settings.TranscriptionEngine;
                if (preloadBackend == TranscriptionBackend.Gpu && !cudaRuntimeService.IsRuntimeInstalled())
                {
                    // Nie próbujemy wymuszać CUDA bez wymaganych bibliotek. Użytkownik może je
                    // pobrać z ustawień, a po restarcie model zostanie załadowany już na GPU.
                    Log.Warning("GPU was selected, but CUDA libraries are missing. Preloading will use the CPU.");
                    preloadBackend = TranscriptionBackend.Cpu;
                }

                transcriptionService = TranscriptionService.Instance;
                transcriptionService.SetLanguage(settings.TranscribeLanguage);
                if (selectedModel != null && isSelectedModelAvailable)
                {
                    var preloadTask = transcriptionService.StartInitializationAsync(preloadBackend, selectedModel);

                    // Nie blokujemy utworzenia formularza. Obserwujemy wyjątek zadania, a diagnostyka
                    // pozostaje dostępna w singletonie przez State oraz InitializationError.
                    _ = ObservePreloadFailureAsync(preloadTask);
                }
                else
                {
                    Log.Warning("The selected Whisper model {ModelId} is not available in the LLM directory next to the executable.", selectedModel?.Id ?? "not selected");
                    if (selectedModel == null)
                    {
                        MessageBox.Show(
                            UiStrings.Get("WhisperModelSelectionRequired"),
                            UiStrings.Get("ApplicationTitle"),
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }

                Log.Information("Starting the WinForms message loop with backend {Backend}.", preloadBackend);
                using var applicationContext = new TrayApplicationContext(
                    settingsService,
                    transcriptionService,
                    autoStartTaskService,
                    showSettingsAtStartup: !isSelectedModelAvailable);
                Application.Run(applicationContext);
                restartRequested = applicationContext.ShouldRestart;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "The application terminated due to an unhandled error.");
                MessageBox.Show(
                    UiStrings.Format("UnexpectedErrorWithLogPath", Environment.NewLine, LogDirectory),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (restartRequested)
                {
                    Log.Information("=== Restarting VoiceToPaste. ===");
                }
                else
                {
                    Log.Information("=== Shutting down VoiceToPaste. ===");
                }

                transcriptionService?.Dispose();
                Log.CloseAndFlush();
            }

            if (restartRequested)
            {
                StartNewInstance();
            }
        }

        private static string GetApplicationVersion()
        {
            var version = typeof(Program).Assembly.GetName().Version;
            if (version == null)
            {
                return "unknown";
            }

            return version.ToString(3);
        }

        /// <summary>
        /// Uruchamia następną instancję dopiero po zamknięciu pętli WinForms, zwolnieniu
        /// zasobów i mutexa. Zapobiega to odrzuceniu restartu jako drugiej instancji.
        /// </summary>
        private static void StartNewInstance()
        {
            try
            {
                var executablePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    throw LocalizedExceptionFactory.InvalidOperation("ApplicationExecutablePathUnavailable");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    UiStrings.Format("ApplicationRestartFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Obserwuje zadanie preloadu, aby błąd modelu nie został nieobsłużonym wyjątkiem w tle.
        /// Stan Failed i szczegóły błędu ustawia już TranscriptionService.
        /// </summary>
        private static async Task ObservePreloadFailureAsync(Task preloadTask)
        {
            try
            {
                await preloadTask;
            }
            catch (Exception ex)
            {
                // Błąd stanu został zapisany w TranscriptionService; tutaj zapisujemy obserwację
                // zadania, aby wyjątek tła nie zniknął bez śladu.
                Log.Debug(ex, "The preload task failed.");
            }
        }

        private static void ConfigureLogger()
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        Path.Combine(LogDirectory, "VoiceToPaste.log"),
                        rollingInterval: RollingInterval.Infinite,
                        fileSizeLimitBytes: 10 * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: 1,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                // Brak prawa zapisu obok EXE nie może uniemożliwić uruchomienia aplikacji.
                Log.Logger = new LoggerConfiguration().MinimumLevel.Information().CreateLogger();
                Log.Error(ex, "Failed to configure logging in the application directory.");
            }
        }
    }
}
