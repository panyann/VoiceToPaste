using Serilog;
using VoiceToPaste.Models;

namespace VoiceToPaste.Services
{
    /// <summary>Outcome of evaluating a Whisper engine change; the form acts on it.</summary>
    internal enum WhisperEngineChangeStep
    {
        /// <summary>The selected engine is already stored in settings.</summary>
        NoChange,

        /// <summary>GPU was selected, but the CUDA libraries are not installed.</summary>
        RequiresCudaInstall,

        /// <summary>The change can be saved.</summary>
        ReadyToSave
    }

    /// <summary>
    /// A tool for the settings form: evaluates a Whisper engine change and saves it
    /// with rollback. It never opens windows and never requests a restart — those
    /// decisions belong to the form, which acts on the returned result.
    /// </summary>
    internal sealed class WhisperEngineChangeWorkflow
    {
        private static readonly ILogger Logger = Log.ForContext<WhisperEngineChangeWorkflow>();
        private readonly AppSettings _settings;
        private readonly SettingsService _settingsService;

        internal WhisperEngineChangeWorkflow(SettingsService settingsService)
        {
            ArgumentNullException.ThrowIfNull(settingsService);

            _settingsService = settingsService;
            _settings = settingsService.Settings;
        }

        /// <summary>
        /// Pure decision without side effects: does not mutate settings and does not touch the file.
        /// </summary>
        internal WhisperEngineChangeStep EvaluateChange(TranscriptionBackend selectedEngine, bool cudaInstalled)
        {
            if (_settings.TranscriptionEngine == selectedEngine)
                return WhisperEngineChangeStep.NoChange;

            if (selectedEngine == TranscriptionBackend.Gpu && !cudaInstalled)
                return WhisperEngineChangeStep.RequiresCudaInstall;

            return WhisperEngineChangeStep.ReadyToSave;
        }

        /// <summary>
        /// Sets the new engine and saves the settings file. A failed save restores the
        /// previous in-memory engine before the exception reaches the caller.
        /// </summary>
        internal void SaveChange(TranscriptionBackend selectedEngine)
        {
            var previousEngine = _settings.TranscriptionEngine;
            _settings.TranscriptionEngine = selectedEngine;
            try
            {
                _settingsService.Save();
            }
            catch
            {
                _settings.TranscriptionEngine = previousEngine;
                throw;
            }

            Logger.Information("Saved the Whisper engine selection {Engine}.", selectedEngine);
        }
    }
}
