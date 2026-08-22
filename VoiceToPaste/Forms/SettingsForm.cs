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
        private readonly WhisperModelDownloadService _modelDownloadService = new();
        private readonly AppSettings _settings;
        private readonly HotkeyCaptureController _hotkeyCapture;
        private readonly ContextMenuStrip _recordingLimitContextMenu = new();
        private readonly System.Windows.Forms.Timer _recordingLimitSaveTimer = new() { Interval = 1000 };
        private readonly List<ModelSelectionOption> _modelOptions =
            [new(null), .. WhisperModelCatalog.GetAll().Select(model => new ModelSelectionOption(model))];
        private bool _changingEngineSelection;
        private bool _changingLanguageSelection;
        private bool _changingUiLanguageSelection;
        private bool _changingModelSelection;
        private bool _restoringRecordingLimit;

        public event Action<bool>? TesterVisibilityChanged;
        public event Action<bool>? HotkeyCaptureVisibilityChanged;
        public event Func<HotkeyGesture?, string?>? HotkeyChangeRequested;
        public event Action<int>? RecordingLimitChanged;
        public event Action? RestartRequested;

        public SettingsForm(
            SettingsService settingsService,
            AppSettings settings,
            AutoStartTaskService autoStartTaskService)
        {
            _settingsService = settingsService;
            _autoStartTaskService = autoStartTaskService;
            _settings = settings;
            InitializeComponent();
            var productVersion = Application.ProductVersion;
            var metadataSeparatorIndex = productVersion.IndexOf('+');
            var displayedVersion = metadataSeparatorIndex >= 0
                ? productVersion[..metadataSeparatorIndex]
                : productVersion;
            Text = UiStrings.Format("SettingsWindowTitleWithVersion", Text, displayedVersion);
            InitializeEngineCombo();
            InitializeLanguageCombo();
            InitializeUiLanguageCombo();
            InitializeModelCombo();

            SetSelectedBackend(_settings.TranscriptionEngine);
            SetSelectedLanguage(_settings.TranscribeLanguage);
            SetSelectedUiLanguage(_settings.UiLanguage);
            SetSelectedModel(_settings.WhisperModelId);
            checkBoxStartInTray.Checked = _settings.StartInTray;
            checkBoxAutoStart.Checked = _settings.AutoStart;
            toolTipAutoStart.SetToolTip(
                checkBoxAutoStart,
                UiStrings.Get("AutoStartDisableBeforeMovingApplicationTooltip"));
            textBoxRecordLimit.Text = _settings.RecordingLimitSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // Puste menu usuwa standardową opcję „Wklej”, aby pole przyjmowało tylko dane wpisane z klawiatury.
            textBoxRecordLimit.ContextMenuStrip = _recordingLimitContextMenu;
            _recordingLimitSaveTimer.Tick += recordingLimitSaveTimer_Tick;
            _hotkeyCapture = new HotkeyCaptureController(textBoxHotKey);
            _hotkeyCapture.SetHotkey(_settings.Hotkey);
            _hotkeyCapture.GestureCaptured += HotkeyCapture_GestureCaptured;
            _hotkeyCapture.CaptureStateChanged += HotkeyCapture_CaptureStateChanged;
            comboBoxEngine.SelectedIndexChanged += comboBoxEngine_SelectedIndexChanged;
            comboBoxLanguage.SelectionChangeCommitted += comboBoxLanguage_SelectionChangeCommitted;
            comboBoxUiLanguage.SelectionChangeCommitted += comboBoxUiLanguage_SelectionChangeCommitted;
            comboBoxModel.SelectionChangeCommitted += comboBoxModel_SelectionChangeCommitted;
            checkBoxStartInTray.CheckedChanged += checkBoxStartInTray_CheckedChanged;
            checkBoxAutoStart.CheckedChanged += checkBoxAutoStart_CheckedChanged;
            textBoxRecordLimit.TextChanged += textBoxRecordLimit_TextChanged;
            textBoxRecordLimit.Leave += textBoxRecordLimit_Leave;
            Shown += SettingsForm_Shown;
        }

        // Wypełniamy combo w kodzie zamiast w Designerze — łatwiej utrzymać mapowanie na enum.
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
        /// Zmiana modelu jest zatwierdzana dopiero po pobraniu jego pliku. Anulowanie dialogu
        /// przywraca poprzednią wartość, dzięki czemu ustawienia nigdy nie wskazują braku modelu.
        /// </summary>
        private async void comboBoxModel_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            if (_changingModelSelection)
                return;

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

            RestoreSelectedModel(_settings.WhisperModelId);
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
                _settingsService.Save(_settings);
                Logger.Information("Saved the Whisper model selection {ModelId}. Restarting the application.", selectedModel?.Id ?? "not selected");
                RestartRequested?.Invoke();
            }
            catch (Exception ex)
            {
                _settings.WhisperModelId = previousModelId;
                RestoreSelectedModel(previousModelId);
                Logger.Error(ex, "Failed to save the Whisper model selection.");
                MessageBox.Show(
                    this,
                    UiStrings.Format("SettingsSaveFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void comboBoxEngine_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_changingEngineSelection)
                return;

            var selectedBackend = SelectedBackend;
            if (_settings.TranscriptionEngine == selectedBackend)
                return;

            var previousBackend = _settings.TranscriptionEngine;
            Logger.Information("The user is changing the backend from {PreviousBackend} to {SelectedBackend}.", previousBackend, selectedBackend);

            if (selectedBackend == TranscriptionBackend.Gpu && !EnsureCudaRuntimeInstalled())
            {
                Logger.Information("The backend change to GPU was reverted because CUDA was not installed.");
                RestoreSelectedBackend(previousBackend);
                return;
            }

            _settings.TranscriptionEngine = selectedBackend;

            try
            {
                _settingsService.Save(_settings);
                Logger.Information("Saved the backend selection {Backend}. Restarting the application.", selectedBackend);
                RestartRequested?.Invoke();
            }
            catch (Exception ex)
            {
                // Zmieniamy z powrotem widok i model, żeby UI nie sugerowało zapisu, który się nie udał.
                _settings.TranscriptionEngine = previousBackend;
                RestoreSelectedBackend(previousBackend);
                Logger.Error(ex, "Failed to save the backend selection.");
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
            if (_changingLanguageSelection)
                return;

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
                _settingsService.Save(_settings);
                TranscriptionService.Instance.SetLanguage(selectedLanguage);
                Logger.Information("Saved the transcription language selection {Language}.", selectedLanguage);
            }
            catch (Exception ex)
            {
                // Cofamy widok i model, aby UI nie sugerowało zapisu, który się nie udał.
                _settings.TranscribeLanguage = previousLanguage;
                RestoreSelectedLanguage(previousLanguage);
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
        /// Zapisuje nowy język przed restartem. Nieudany zapis przywraca poprzednią wartość,
        /// aby formularz i plik settings.yaml nie pokazywały rozbieżnego stanu.
        /// </summary>
        private void comboBoxUiLanguage_SelectionChangeCommitted(object? sender, EventArgs e)
        {
            if (_changingUiLanguageSelection)
            {
                return;
            }

            var selectedLanguage = GetSelectedUiLanguage();
            if (string.Equals(_settings.UiLanguage, selectedLanguage, StringComparison.Ordinal))
            {
                return;
            }

            var previousLanguage = _settings.UiLanguage;
            _settings.UiLanguage = selectedLanguage;

            try
            {
                _settingsService.Save(_settings);
                Logger.Information("Saved the interface language {Language}. Restarting the application.", selectedLanguage);
                RestartRequested?.Invoke();
            }
            catch (Exception ex)
            {
                _settings.UiLanguage = previousLanguage;
                RestoreSelectedUiLanguage(previousLanguage);
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
                _settingsService.Save(_settings);
                Logger.Information("Saved the start-in-tray setting: {StartInTray}.", startInTray);
            }
            catch (Exception ex)
            {
                // Cofamy model przed zmianą kontrolki, aby kolejne zdarzenie nie próbowało ponownie zapisywać ustawień.
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
        /// Aktualizuje najpierw zadanie systemowe, a następnie YAML. Gdy zapis YAML się nie uda,
        /// przywraca również poprzednią konfigurację zadania, aby oba źródła pozostały zgodne.
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
                _settingsService.Save(_settings);
                Logger.Information("Saved the autostart setting: {AutoStart}.", autoStart);
            }
            catch (Exception ex)
            {
                _settings.AutoStart = previousAutoStart;

                if (taskUpdated)
                    RestoreAutoStartTask(previousAutoStart);

                // Model jest już cofnięty, więc zdarzenie wywołane zmianą kontrolki zakończy się bez operacji.
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
                // Pierwotny błąd nadal trafia do użytkownika, a ten wpis zachowuje szczegóły nieudanego rollbacku.
                Logger.Error(ex, "Failed to restore the previous autostart task configuration.");
            }
        }

        private void SettingsForm_Shown(object? sender, EventArgs e)
        {
            Logger.Information("The settings window was opened.");
            // Ustawienie mogło zostać zapisane przed aktualizacją aplikacji. Wtedy preload
            // bezpiecznie używa CPU, a użytkownik nadal może świadomie pobrać runtime CUDA.
            if (_settings.TranscriptionEngine == TranscriptionBackend.Gpu && !_cudaRuntimeService.IsRuntimeInstalled())
            {
                if (EnsureCudaRuntimeInstalled())
                {
                    Logger.Information("CUDA was installed for the existing GPU setting. Restarting the application.");
                    RestartRequested?.Invoke();
                }
            }

        }

        private bool EnsureCudaRuntimeInstalled()
        {
            if (_cudaRuntimeService.IsRuntimeInstalled())
                return true;

            Logger.Information("Opening the CUDA installation window.");
            using var downloadForm = new CudaRuntimeDownloadForm();
            var installed = downloadForm.ShowDialog(this) == DialogResult.OK;
            Logger.Information("The CUDA installation window was closed. Success: {Installed}.", installed);
            return installed;
        }

        private void RestoreSelectedBackend(TranscriptionBackend backend)
        {
            _changingEngineSelection = true;
            try
            {
                SetSelectedBackend(backend);
            }
            finally
            {
                _changingEngineSelection = false;
            }
        }

        private void RestoreSelectedLanguage(string language)
        {
            _changingLanguageSelection = true;
            try
            {
                SetSelectedLanguage(language);
            }
            finally
            {
                _changingLanguageSelection = false;
            }
        }

        private void RestoreSelectedUiLanguage(string language)
        {
            _changingUiLanguageSelection = true;
            try
            {
                SetSelectedUiLanguage(language);
            }
            finally
            {
                _changingUiLanguageSelection = false;
            }
        }

        private void RestoreSelectedModel(string? modelId)
        {
            _changingModelSelection = true;
            try
            {
                SetSelectedModel(modelId);
            }
            finally
            {
                _changingModelSelection = false;
            }
        }

        private void btnTester_Click(object sender, EventArgs e)
        {
            // ShowDialog nie zwalnia formularza automatycznie, więc using gwarantuje zwolnienie rejestratora.
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

        /// <summary>Pozycja listy modeli; brak modelu jest prawidłowym stanem konfiguracji.</summary>
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
        /// Zapisuje limit dopiero po sprawdzeniu całej zawartości pola, ponieważ użytkownik
        /// może wkleić tekst z pominięciem ograniczenia klawiszy.
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
                _settingsService.Save(_settings);
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
            using var keyWordsForm = new KeyWordsForm(_settingsService, _settings);
            keyWordsForm.ShowDialog(this);
        }

        private void menuItemAbout_Click(object sender, EventArgs e)
        {
            using var aboutForm = new AboutForm();
            aboutForm.ShowDialog(this);
        }
    }
}
