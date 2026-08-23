using DarkModeForms;
using Serilog;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;
using VoiceToPaste.Services;

namespace VoiceToPaste.Forms
{
    public partial class SettingsForm : Form
    {
        private static readonly ILogger Logger = Log.ForContext<SettingsForm>();
        private readonly SettingsService _settingsService;
        private readonly AutoStartTaskService _autoStartTaskService;
        private readonly CudaRuntimeService _cudaRuntimeService = new();
        private readonly WhisperEngineChangeWorkflow _whisperEngineChangeWorkflow;
        private readonly WhisperModelDownloadService _modelDownloadService = new();
        private readonly AppSettings _settings;
        private readonly HotkeyCaptureController _hotkeyCapture;
        private readonly ContextMenuStrip _recordingLimitContextMenu = new();
        private readonly System.Windows.Forms.Timer _recordingLimitSaveTimer = new() { Interval = 1000 };
        private readonly List<ModelSelectionOption> _modelOptions =
            [new(null), .. WhisperModelCatalog.GetAll().Select(model => new ModelSelectionOption(model))];
        private bool _restoringRecordingLimit;

        public event Action<bool>? TesterVisibilityChanged;
        public event Action<bool>? HotkeyCaptureVisibilityChanged;
        public event Func<HotkeyGesture?, string?>? HotkeyChangeRequested;
        public event Action<int>? RecordingLimitChanged;
        public event Action? RestartRequested;

        private DarkModeCS dm = null;

        public SettingsForm(
            SettingsService settingsService,
            AutoStartTaskService autoStartTaskService)
        {
            _settingsService = settingsService;
            _autoStartTaskService = autoStartTaskService;
            _settings = settingsService.Settings;
            _whisperEngineChangeWorkflow = new WhisperEngineChangeWorkflow(_settingsService);
            InitializeComponent();
            ApplyVersionToTitle();
            InitializeEngineCombo();
            InitializeLanguageCombo();
            InitializeUiLanguageCombo();
            InitializeModelCombo();
            ApplySettingsToControls();
            InitializeRecordingLimitControls();
            _hotkeyCapture = CreateHotkeyCapture();

            dm = new DarkModeCS(this)
            {
                //[Optional] Choose your preferred color mode here:
                ColorMode = DarkModeCS.DisplayMode.SystemDefault
            };
        }

        // Numer wersji zawiera metadane commitu po '+'; w tytule pokazujemy sam numer wersji.
        private void ApplyVersionToTitle()
        {
            var productVersion = Application.ProductVersion;
            var metadataSeparatorIndex = productVersion.IndexOf('+');
            var displayedVersion = metadataSeparatorIndex >= 0
                ? productVersion[..metadataSeparatorIndex]
                : productVersion;
            Text = UiStrings.Format("SettingsWindowTitleWithVersion", Text, displayedVersion);
        }

        // We fill the combos in code instead of the Designer — it is easier to keep the enum mapping.
        private void InitializeEngineCombo()
        {
            comboBoxEngine.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxEngine.Items.AddRange([
                UiStrings.Get("TranscriptionEngineAuto"),
                UiStrings.Get("TranscriptionEngineGpu"),
                UiStrings.Get("TranscriptionEngineCpu")]);
        }

        private void InitializeLanguageCombo()
        {
            comboBoxLanguage.DisplayMember = nameof(TranscriptionLanguageOption.DisplayName);
            comboBoxLanguage.ValueMember = nameof(TranscriptionLanguageOption.Code);
            comboBoxLanguage.DataSource = TranscriptionLanguages.GetOptions().ToList();
        }

        private void InitializeUiLanguageCombo()
        {
            comboBoxUiLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxUiLanguage.DisplayMember = nameof(UiLanguageOption.DisplayName);
            comboBoxUiLanguage.ValueMember = nameof(UiLanguageOption.Code);
            comboBoxUiLanguage.DataSource = UiLanguages.GetOptions().ToList();
        }

        private void InitializeModelCombo()
        {
            comboBoxModel.DisplayMember = nameof(ModelSelectionOption.DisplayName);
            comboBoxModel.DataSource = _modelOptions;
        }

        private void ApplySettingsToControls()
        {
            SetSelectedBackend(_settings.TranscriptionEngine);
            SetSelectedLanguage(_settings.TranscribeLanguage);
            SetSelectedUiLanguage(_settings.UiLanguage);
            SetSelectedModel(_settings.WhisperModelId);
            checkBoxStartInTray.Checked = _settings.StartInTray;
            checkBoxAutoStart.Checked = _settings.AutoStart;
            toolTipAutoStart.SetToolTip(
                checkBoxAutoStart,
                UiStrings.Get("AutoStartDisableBeforeMovingApplicationTooltip"));
            // The restore guard blocks TextChanged so startup does not arm the limit save timer.
            RestoreRecordingLimit(_settings.RecordingLimitSeconds);
        }

        private void InitializeRecordingLimitControls()
        {
            // An empty menu removes the default "Paste" option so the field only accepts typed input.
            textBoxRecordLimit.ContextMenuStrip = _recordingLimitContextMenu;
            _recordingLimitSaveTimer.Tick += recordingLimitSaveTimer_Tick;
        }

        private HotkeyCaptureController CreateHotkeyCapture()
        {
            var capture = new HotkeyCaptureController(textBoxHotKey);
            capture.SetHotkey(_settings.Hotkey);
            capture.GestureCaptured += HotkeyCapture_GestureCaptured;
            capture.CaptureStateChanged += HotkeyCapture_CaptureStateChanged;
            return capture;
        }

        private TranscriptionBackend SelectedBackend => comboBoxEngine.SelectedIndex switch
        {
            1 => TranscriptionBackend.Gpu,
            2 => TranscriptionBackend.Cpu,
            _ => TranscriptionBackend.Auto,
        };

        private void SetSelectedBackend(TranscriptionBackend backend)
        {
            comboBoxEngine.SelectedIndex = backend switch
            {
                TranscriptionBackend.Gpu => 1,
                TranscriptionBackend.Cpu => 2,
                _ => 0,
            };
        }

        private string SelectedLanguage =>
            (comboBoxLanguage.SelectedItem as TranscriptionLanguageOption)?.Code
            ?? TranscriptionLanguages.GetSystemDefaultCode();

        private void SetSelectedLanguage(string language)
        {
            comboBoxLanguage.SelectedValue = TranscriptionLanguages.Normalize(language);
        }

        private string GetSelectedUiLanguage()
        {
            if (comboBoxUiLanguage.SelectedItem is UiLanguageOption selectedOption)
            {
                return selectedOption.Code;
            }

            return UiLanguages.GetSystemDefaultCode();
        }

        private void SetSelectedUiLanguage(string language)
        {
            comboBoxUiLanguage.SelectedValue = UiLanguages.Normalize(language);
        }

        private WhisperModel? SelectedModel =>
            (comboBoxModel.SelectedItem as ModelSelectionOption)?.Model;

        private void SetSelectedModel(string? modelId)
        {
            var selectedIndex = _modelOptions.FindIndex(option =>
                string.Equals(option.Model?.Id, modelId, StringComparison.Ordinal));
            comboBoxModel.SelectedIndex = selectedIndex < 0 ? 0 : selectedIndex;
        }

        /// <summary>
        /// A model change is committed only after its file is downloaded. Cancelling the dialog
        /// restores the previous value, so settings never point to a missing model.
        /// </summary>
        private async void comboBoxModel_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            var selectedModel = SelectedModel;
            if (string.Equals(_settings.WhisperModelId, selectedModel?.Id, StringComparison.Ordinal))
                return;

            if (selectedModel == null)
            {
                SaveModelAndRestart(null);
                return;
            }

            if (await _modelDownloadService.IsModelValidAsync(selectedModel))
            {
                SaveModelAndRestart(selectedModel);
                return;
            }

            if (DownloadSelectedModel(selectedModel))
                return;

            SetSelectedModel(_settings.WhisperModelId);
        }

        private bool DownloadSelectedModel(WhisperModel selectedModel)
        {
            using var downloadForm = new WhisperModelDownloadForm(selectedModel, _modelDownloadService);
            if (downloadForm.ShowDialog(this) != DialogResult.OK)
                return false;

            SaveModelAndRestart(selectedModel);
            return true;
        }

        private void SaveModelAndRestart(WhisperModel? selectedModel)
        {
            var previousModelId = _settings.WhisperModelId;
            _settings.WhisperModelId = selectedModel?.Id;
            try
            {
                _settingsService.Save();
                Logger.Information("Saved the Whisper model selection {ModelId}. Restarting the application.", selectedModel?.Id ?? "not selected");
                RestartRequested?.Invoke();
            }
            catch (Exception ex)
            {
                _settings.WhisperModelId = previousModelId;
                SetSelectedModel(previousModelId);
                Logger.Error(ex, "Failed to save the Whisper model selection.");
                MessageBox.Show(
                    this,
                    UiStrings.Format("SettingsSaveFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void comboBoxEngine_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            var selectedEngine = SelectedBackend;
            var previousEngine = _settings.TranscriptionEngine;
            var step = _whisperEngineChangeWorkflow.EvaluateChange(selectedEngine, _cudaRuntimeService.IsRuntimeInstalled());
            if (step == WhisperEngineChangeStep.NoChange)
                return;

            Logger.Information("The user is changing the Whisper engine from {PreviousEngine} to {SelectedEngine}.", previousEngine, selectedEngine);

            if (step == WhisperEngineChangeStep.RequiresCudaInstall)
            {
                var cudaInstalled = InstallCudaRuntime();
                if (!cudaInstalled)
                {
                    Logger.Information("The Whisper engine change was reverted because CUDA installation was not completed.");
                    SetSelectedBackend(previousEngine);
                    return;
                }
            }
            else if (step != WhisperEngineChangeStep.ReadyToSave)
            {
                return;
            }

            try
            {
                _whisperEngineChangeWorkflow.SaveChange(selectedEngine);
                Logger.Information("Restarting the application.");
                ShowWhisperEngineRestartInfo();
                RestartRequested?.Invoke();
            }
            catch (Exception ex)
            {
                // The workflow has already restored the in-memory engine; restore the view as well.
                SetSelectedBackend(previousEngine);
                Logger.Error(ex, "Failed to save the Whisper engine selection.");
                MessageBox.Show(
                    this,
                    UiStrings.Format("SettingsSaveFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void comboBoxLanguage_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            var selectedLanguage = SelectedLanguage;
            if (_settings.TranscribeLanguage == selectedLanguage)
                return;

            var previousLanguage = _settings.TranscribeLanguage;
            Logger.Information("The user is changing the transcription language from {PreviousLanguage} to {SelectedLanguage}.",
                previousLanguage,
                selectedLanguage);
            _settings.TranscribeLanguage = selectedLanguage;

            try
            {
                _settingsService.Save();
                TranscriptionService.Instance.SetLanguage(selectedLanguage);
                Logger.Information("Saved the transcription language selection {Language}.", selectedLanguage);
            }
            catch (Exception ex)
            {
                // Revert the view and the model so the UI does not suggest a save that failed.
                _settings.TranscribeLanguage = previousLanguage;
                SetSelectedLanguage(previousLanguage);
                Logger.Error(ex, "Failed to save the transcription language selection.");
                MessageBox.Show(
                    this,
                    UiStrings.Format("SettingsSaveFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Saves the new language before the restart. A failed save restores the previous value
        /// so the form and settings.yaml do not show a divergent state.
        /// </summary>
        private void comboBoxUiLanguage_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            var selectedLanguage = GetSelectedUiLanguage();
            if (string.Equals(_settings.UiLanguage, selectedLanguage, StringComparison.Ordinal))
            {
                return;
            }

            var previousLanguage = _settings.UiLanguage;
            _settings.UiLanguage = selectedLanguage;

            try
            {
                _settingsService.Save();
                Logger.Information("Saved the interface language {Language}. Restarting the application.", selectedLanguage);
                RestartRequested?.Invoke();
            }
            catch (Exception ex)
            {
                _settings.UiLanguage = previousLanguage;
                SetSelectedUiLanguage(previousLanguage);
                Logger.Error(ex, "Failed to save the interface language.");
                MessageBox.Show(
                    this,
                    UiStrings.Format("SettingsSaveFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void checkBoxStartInTray_CheckedChanged(object? sender, EventArgs e)
        {
            var startInTray = checkBoxStartInTray.Checked;
            if (_settings.StartInTray == startInTray)
                return;

            var previousStartInTray = _settings.StartInTray;
            _settings.StartInTray = startInTray;

            try
            {
                _settingsService.Save();
                Logger.Information("Saved the start-in-tray setting: {StartInTray}.", startInTray);
            }
            catch (Exception ex)
            {
                // Revert the model before changing the control so the next event does not retry the save.
                _settings.StartInTray = previousStartInTray;
                checkBoxStartInTray.Checked = previousStartInTray;
                Logger.Error(ex, "Failed to save the start-in-tray setting.");
                MessageBox.Show(
                    this,
                    UiStrings.Format("SettingsSaveFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Updates the scheduled task first, then the YAML. When the YAML save fails it also
        /// restores the previous task configuration so both sources stay consistent.
        /// </summary>
        private void checkBoxAutoStart_CheckedChanged(object? sender, EventArgs e)
        {
            var autoStart = checkBoxAutoStart.Checked;
            if (_settings.AutoStart == autoStart)
                return;

            var previousAutoStart = _settings.AutoStart;
            var taskUpdated = false;

            try
            {
                SetAutoStartTask(autoStart);
                taskUpdated = true;

                _settings.AutoStart = autoStart;
                _settingsService.Save();
                Logger.Information("Saved the autostart setting: {AutoStart}.", autoStart);
            }
            catch (Exception ex)
            {
                _settings.AutoStart = previousAutoStart;

                if (taskUpdated)
                    RestoreAutoStartTask(previousAutoStart);

                // The model is already reverted, so the event fired by the control change ends without an action.
                checkBoxAutoStart.Checked = previousAutoStart;
                Logger.Error(ex, "Failed to change the autostart setting to {AutoStart}.", autoStart);
                MessageBox.Show(
                    this,
                    UiStrings.Format("AutoStartChangeFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SetAutoStartTask(bool enabled)
        {
            if (enabled)
                _autoStartTaskService.Enable();
            else
                _autoStartTaskService.Disable();
        }

        private void RestoreAutoStartTask(bool enabled)
        {
            try
            {
                SetAutoStartTask(enabled);
            }
            catch (Exception ex)
            {
                // The original error still reaches the user; this entry keeps the failed rollback details.
                Logger.Error(ex, "Failed to restore the previous autostart task configuration.");
            }
        }

        private void SettingsForm_Shown(object? sender, EventArgs e)
        {
            Logger.Information("The settings window was opened.");
            // The setting may have been saved before the application update. Then preloading
            // safely uses the CPU while the user can still deliberately download the CUDA runtime.
            if (_settings.TranscriptionEngine != TranscriptionBackend.Gpu)
                return;

            if (_cudaRuntimeService.IsRuntimeInstalled())
                return;

            if (!InstallCudaRuntime())
                return;

            Logger.Information("CUDA was installed for the existing GPU setting. Restarting the application.");
            ShowWhisperEngineRestartInfo();
            RestartRequested?.Invoke();
        }

        private void ShowWhisperEngineRestartInfo()
        {
            MessageBox.Show(
                this,
                UiStrings.Get("WhisperEngineRestartInfo"),
                UiStrings.Get("ApplicationTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private bool InstallCudaRuntime()
        {
            Logger.Information("Opening the CUDA installation window.");
            using var downloadForm = new CudaRuntimeDownloadForm(_cudaRuntimeService);
            var installed = downloadForm.ShowDialog(this) == DialogResult.OK;
            Logger.Information("The CUDA installation window was closed. Success: {Installed}.", installed);
            return installed;
        }

        private void btnTester_Click(object sender, EventArgs e)
        {
            // ShowDialog does not dispose the form automatically, so using guarantees the recorder is released.
            Logger.Information("Opening the transcription tester.");
            using var testerForm = new TesterForm(_settings);
            TesterVisibilityChanged?.Invoke(true);
            try
            {
                testerForm.ShowDialog(this);
            }
            finally
            {
                TesterVisibilityChanged?.Invoke(false);
            }
        }

        private void HotkeyCapture_GestureCaptured(HotkeyGesture? hotkey)
        {
            if (HotkeyChangeRequested == null)
            {
                var unavailableMessage = UiStrings.Get("GlobalHotkeyServiceUnavailable");
                _hotkeyCapture.SetHotkey(_settings.Hotkey);
                Logger.Warning("Failed to change the global hotkey because the registration service is unavailable.");
                MessageBox.Show(this, unavailableMessage, UiStrings.Get("ApplicationTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var errorMessage = HotkeyChangeRequested(hotkey);

            _hotkeyCapture.SetHotkey(_settings.Hotkey);
            if (errorMessage == null)
            {
                Logger.Information("Changed the global hotkey to {Hotkey}.", _settings.Hotkey?.ToDisplayString() ?? "disabled");
                return;
            }

            Logger.Warning("Failed to change the global hotkey.");
            MessageBox.Show(this, errorMessage, UiStrings.Get("ApplicationTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void HotkeyCapture_CaptureStateChanged(bool isCapturing)
        {
            HotkeyCaptureVisibilityChanged?.Invoke(isCapturing);
        }

        /// <summary>Position in the model list; a missing model is a valid configuration state.</summary>
        private sealed class ModelSelectionOption
        {
            public ModelSelectionOption(WhisperModel? model)
            {
                Model = model;
            }

            public WhisperModel? Model { get; }
            public string DisplayName => Model?.DisplayName ?? UiStrings.Get("WhisperModelNotSelected");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Logger.Information("The settings window was closed.");
            _recordingLimitSaveTimer.Dispose();
            _recordingLimitContextMenu.Dispose();
            _hotkeyCapture.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveRecordingLimit(showValidationError: false, restoreInvalidValue: true);
            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
                SaveRecordingLimit(showValidationError: false, restoreInvalidValue: true);

            base.OnResize(e);
        }

        private void textBoxRecordingTime_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert))
            {
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SaveRecordingLimit(showValidationError: true, restoreInvalidValue: true);
                return;
            }

            var isTopRowDigit = e.KeyCode is >= Keys.D0 and <= Keys.D9 && !e.Shift;
            var isNumpadDigit = e.KeyCode is >= Keys.NumPad0 and <= Keys.NumPad9;
            var isEditingOrNavigationKey = e.KeyCode is Keys.Back or Keys.Delete or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Tab;
            var isClipboardShortcut = e.Control && (e.KeyCode is Keys.A or Keys.C or Keys.X);
            if (isTopRowDigit || isNumpadDigit || isEditingOrNavigationKey || isClipboardShortcut)
                return;

            e.SuppressKeyPress = true;
        }

        private void textBoxRecordLimit_Leave(object? sender, EventArgs e)
        {
            SaveRecordingLimit(showValidationError: false, restoreInvalidValue: true);
        }

        private void textBoxRecordLimit_TextChanged(object? sender, EventArgs e)
        {
            if (_restoringRecordingLimit)
                return;

            _recordingLimitSaveTimer.Stop();
            _recordingLimitSaveTimer.Start();
        }

        private void recordingLimitSaveTimer_Tick(object? sender, EventArgs e)
        {
            _recordingLimitSaveTimer.Stop();
            SaveRecordingLimit(showValidationError: false, restoreInvalidValue: false);
        }

        /// <summary>
        /// Saves the limit only after checking the whole field content, because the user
        /// can paste text bypassing the key restriction.
        /// </summary>
        private void SaveRecordingLimit(bool showValidationError, bool restoreInvalidValue)
        {
            _recordingLimitSaveTimer.Stop();

            if (!int.TryParse(
                    textBoxRecordLimit.Text.Trim(),
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var recordingLimitSeconds) ||
                recordingLimitSeconds < AppSettings.MinimumRecordingLimitSeconds ||
                recordingLimitSeconds > AppSettings.MaximumRecordingLimitSeconds)
            {
                if (restoreInvalidValue)
                    RestoreRecordingLimit(_settings.RecordingLimitSeconds);

                if (showValidationError)
                {
                    MessageBox.Show(
                        this,
                        UiStrings.Format(
                            "RecordingLimitValidationError",
                            AppSettings.MinimumRecordingLimitSeconds,
                            AppSettings.MaximumRecordingLimitSeconds),
                        UiStrings.Get("ApplicationTitle"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                return;
            }

            if (_settings.RecordingLimitSeconds == recordingLimitSeconds)
            {
                RestoreRecordingLimit(recordingLimitSeconds);
                return;
            }

            var previousRecordingLimitSeconds = _settings.RecordingLimitSeconds;
            _settings.RecordingLimitSeconds = recordingLimitSeconds;
            try
            {
                _settingsService.Save();
            }
            catch (Exception ex)
            {
                _settings.RecordingLimitSeconds = previousRecordingLimitSeconds;
                RestoreRecordingLimit(previousRecordingLimitSeconds);
                Logger.Error(ex, "Failed to save the recording limit.");
                MessageBox.Show(
                    this,
                    UiStrings.Format("SettingsSaveFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            RecordingLimitChanged?.Invoke(recordingLimitSeconds);
            Logger.Information("Saved the recording limit of {RecordingLimitSeconds} seconds.", recordingLimitSeconds);
        }

        private void RestoreRecordingLimit(int recordingLimitSeconds)
        {
            _recordingLimitSaveTimer.Stop();
            _restoringRecordingLimit = true;
            try
            {
                textBoxRecordLimit.Text = recordingLimitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            finally
            {
                _restoringRecordingLimit = false;
            }
        }

        private void btnKeyWords_Click(object sender, EventArgs e)
        {
            using var keyWordsForm = new KeyWordsForm(_settingsService);
            keyWordsForm.ShowDialog(this);
        }

        private void menuItemAbout_Click(object sender, EventArgs e)
        {
            using var aboutForm = new AboutForm();
            aboutForm.ShowDialog(this);
        }
    }
}
