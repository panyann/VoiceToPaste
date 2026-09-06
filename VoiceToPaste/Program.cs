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
                var settings = SettingsService.Load();

                // We set the culture before WinForms initialization so form resources,
                // messages and tasks created later use the same language.
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

                // The mutex handle stays open until the process ends so a second instance
                // cannot start service initialization or create its own tray icon.
                Log.Information("=== Starting VoiceToPaste, version {ApplicationVersion}. ===",
                    ApplicationVersionService.GetVersion());
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
                    // We do not force CUDA without the required libraries. The user can download
                    // them from Settings, and after a restart the model is loaded on the GPU.
                    Log.Warning("GPU was selected, but CUDA libraries are missing. Preloading will use the CPU.");
                    preloadBackend = TranscriptionBackend.Cpu;
                }

                transcriptionService = TranscriptionService.Instance;
                transcriptionService.SetLanguage(settings.TranscribeLanguage);
                if (selectedModel != null && isSelectedModelAvailable)
                {
                    var preloadTask = transcriptionService.StartInitializationAsync(preloadBackend, selectedModel);

                    // We do not block creating the form. We observe the task exception and the
                    // diagnostics remain available in the singleton through State and InitializationError.
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

        /// <summary>
        /// Starts the next instance only after the WinForms loop, resources and the mutex are
        /// released. This prevents the restart from being rejected as a second instance.
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
        /// Observes the preload task so a model failure is not an unhandled background exception.
        /// The Failed state and error details are already set by TranscriptionService.
        /// </summary>
        private static async Task ObservePreloadFailureAsync(Task preloadTask)
        {
            try
            {
                await preloadTask;
            }
            catch (Exception ex)
            {
                // The state error was stored in TranscriptionService; here we only observe the
                // task so the background exception does not disappear without a trace.
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
                // No write permission next to the EXE must not prevent the application from running.
                Log.Logger = new LoggerConfiguration().MinimumLevel.Information().CreateLogger();
                Log.Error(ex, "Failed to configure logging in the application directory.");
            }
        }
    }
}
