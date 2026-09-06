using System.Drawing;
using Serilog;
using VoiceToPaste.Forms;
using VoiceToPaste.Models;
using VoiceToPaste.Resources;
using VoiceToPaste.Services;

namespace VoiceToPaste
{
    /// <summary>
    /// Manages the application lifetime while it runs in the system tray.
    /// </summary>
    internal sealed class TrayApplicationContext : ApplicationContext
    {
        private readonly SettingsForm _settingsForm;
        private readonly ContextMenuStrip _trayMenu;
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _idleIcon;
        private readonly Icon _recordingIcon;
        private readonly Icon _transcribingIcon;
        private readonly HotkeyService _hotkeyService;
        private readonly DictationController _dictationController;
        private readonly AutoStartTaskService _autoStartTaskService;
        private readonly AppSettings _settings;
        private bool _trayResourcesDisposed;

        private static readonly ILogger Logger = Log.ForContext<TrayApplicationContext>();

        public TrayApplicationContext(
            TranscriptionService transcriptionService,
            AutoStartTaskService autoStartTaskService,
            bool showSettingsAtStartup = false)
        {
            _autoStartTaskService = autoStartTaskService;
            _settings = SettingsService.Settings;
            _dictationController = new DictationController(
                transcriptionService,
                _settings);
            _dictationController.StateChanged += DictationController_StateChanged;
            _dictationController.ErrorOccurred += DictationController_ErrorOccurred;

            _settingsForm = new SettingsForm(autoStartTaskService);
            _settingsForm.Resize += SettingsForm_Resize;
            _settingsForm.FormClosed += SettingsForm_FormClosed;
            _settingsForm.TesterVisibilityChanged += _dictationController.SetActivationsSuspended;
            _settingsForm.HotkeyCaptureVisibilityChanged += _dictationController.SetActivationsSuspended;
            _settingsForm.HotkeyChangeRequested += ChangeHotkey;
            _settingsForm.RecordingLimitChanged += _dictationController.SetRecordingLimit;
            _settingsForm.RestartRequested += SettingsForm_RestartRequested;

            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add(UiStrings.Get("TrayMenuSettings"), null, (_, _) => ShowSettingsForm());
            _trayMenu.Items.Add(UiStrings.Get("TrayMenuExit"), null, (_, _) => ExitApplication());

            _idleIcon = LoadIcon("micro-green.ico");
            _recordingIcon = LoadIcon("micro-red.ico");
            _transcribingIcon = LoadIcon("micro-blue.ico");

            _notifyIcon = new NotifyIcon
            {
                Icon = _idleIcon,
                Text = UiStrings.Get("ApplicationTitle"),
                ContextMenuStrip = _trayMenu,
                Visible = true,
            };
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;

            _hotkeyService = new HotkeyService();
            _hotkeyService.Activated += HotkeyService_Activated;
            RegisterConfiguredHotkey(_settings.Hotkey);

            // WinForms would show the MainForm automatically at the start of the loop,
            // so we control the first display of the form ourselves.
            if (showSettingsAtStartup || !_settings.StartInTray)
                ShowSettingsForm();

            CheckAutoStartConfiguration();
        }

        public bool ShouldRestart { get; private set; }

        /// <summary>
        /// Checks the task only when the user previously enabled autostart.
        /// Declining the repair does not change the stored intent and does not block the app.
        /// </summary>
        private void CheckAutoStartConfiguration()
        {
            if (!_settings.AutoStart)
                return;

            try
            {
                var status = _autoStartTaskService.GetStatus();
                if (status == AutoStartTaskStatus.Configured)
                    return;

                string message;
                if (status == AutoStartTaskStatus.Missing)
                    message = UiStrings.Format("AutoStartTaskMissingRepairPrompt", Environment.NewLine);
                else
                    message = UiStrings.Format("AutoStartTaskOutdatedRepairPrompt", Environment.NewLine);

                var result = MessageBox.Show(
                    _settingsForm.Visible ? _settingsForm : null,
                    message,
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    Logger.Information("The user declined to repair the startup task with status {TaskStatus}.", status);
                    return;
                }

                _autoStartTaskService.Enable();
                Logger.Information("Repaired the startup task whose previous status was {TaskStatus}.", status);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to check or repair the startup task during application startup.");
                MessageBox.Show(
                    _settingsForm.Visible ? _settingsForm : null,
                    UiStrings.Format("AutoStartCheckOrRepairFailed", LocalizedExceptionFactory.GetUserMessage(ex)),
                    UiStrings.Get("ApplicationTitle"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void SettingsForm_Resize(object? sender, EventArgs e)
        {
            if (_settingsForm.WindowState != FormWindowState.Minimized)
                return;

            // Let Windows finish minimizing before hiding the form to avoid changing its native state during WM_SIZE.
            _settingsForm.BeginInvoke(HideMinimizedSettingsForm);
        }

        /// <summary>
        /// Hides the settings form only when it remains minimized after the current window message completes.
        /// </summary>
        private void HideMinimizedSettingsForm()
        {
            if (_settingsForm.IsDisposed || _settingsForm.WindowState != FormWindowState.Minimized)
                return;

            _settingsForm.Hide();
        }

        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                ToggleSettingsForm();
        }

        /// <summary>
        /// Toggles the settings form unless one of its modal dialogs is open.
        /// </summary>
        private void ToggleSettingsForm()
        {
            if (_settingsForm.IsDisposed || HasOpenModalDialog())
                return;

            if (_settingsForm.Visible)
            {
                _settingsForm.Hide();
                return;
            }

            ShowSettingsForm();
        }

        private bool HasOpenModalDialog()
        {
            foreach (Form ownedForm in _settingsForm.OwnedForms)
            {
                if (!ownedForm.IsDisposed && ownedForm.Visible && ownedForm.Modal)
                    return true;
            }

            return false;
        }

        private async void HotkeyService_Activated(object? sender, EventArgs e)
        {
            await _dictationController.HandleActivationAsync();
        }

        private void DictationController_StateChanged(DictationState state)
        {
            _notifyIcon.Icon = state switch
            {
                DictationState.Recording => _recordingIcon,
                DictationState.Transcribing => _transcribingIcon,
                _ => _idleIcon,
            };

            _notifyIcon.Text = state switch
            {
                DictationState.Recording => UiStrings.Get("TrayTooltipRecording"),
                DictationState.Transcribing => UiStrings.Get("TrayTooltipTranscribing"),
                _ => GetIdleTooltip(),
            };
        }

        /// <summary>
        /// Loads the icon deployed next to the application so the tray uses the same ICO
        /// files as the project configuration and publication.
        /// </summary>
        private static Icon LoadIcon(string fileName)
        {
            var iconPath = Path.Combine(ApplicationPaths.Directory, "Assets", "Icons", fileName);
            return new Icon(iconPath);
        }

        private string? ChangeHotkey(HotkeyGesture? hotkey)
        {
            var previousHotkey = _settings.Hotkey;
            if (!_hotkeyService.TryUpdateRegistration(hotkey, out var errorCode))
                return UiStrings.Format("HotkeyRegistrationFailedWithWindowsError", errorCode);

            _settings.Hotkey = hotkey;
            try
            {
                SettingsService.Save();
                _notifyIcon.Text = GetIdleTooltip();
                return null;
            }
            catch (Exception ex)
            {
                _settings.Hotkey = previousHotkey;
                if (!_hotkeyService.TryUpdateRegistration(previousHotkey, out var restoreErrorCode))
                {
                    Logger.Error("Failed to restore the previous hotkey after a save error. Win32 error code: {ErrorCode}.",
                        restoreErrorCode);
                }

                Logger.Error(ex, "Failed to save the new global hotkey.");
                return UiStrings.Format("SettingsSaveFailed", LocalizedExceptionFactory.GetUserMessage(ex));
            }
        }

        private void DictationController_ErrorOccurred(string message)
        {
            _notifyIcon.Text = UiStrings.Get("TrayTooltipDictationError");
            _notifyIcon.ShowBalloonTip(
                timeout: 5000,
                tipTitle: UiStrings.Get("ApplicationTitle"),
                tipText: message,
                tipIcon: ToolTipIcon.Error);
        }

        private void RegisterConfiguredHotkey(HotkeyGesture? hotkey)
        {
            if (hotkey == null)
            {
                _notifyIcon.Text = UiStrings.Get("TrayTooltipHotkeyDisabled");
                return;
            }

            if (_hotkeyService.TryUpdateRegistration(hotkey, out var errorCode))
            {
                _notifyIcon.Text = GetIdleTooltip();
                return;
            }

            _notifyIcon.Text = UiStrings.Get("TrayTooltipHotkeyUnavailable");
            _notifyIcon.ShowBalloonTip(
                timeout: 5000,
                tipTitle: UiStrings.Get("ApplicationTitle"),
                tipText: UiStrings.Format("HotkeyRegistrationFailedForGesture", hotkey.ToDisplayString(), errorCode),
                tipIcon: ToolTipIcon.Warning);
        }

        private string GetIdleTooltip()
        {
            if (_settings.Hotkey == null)
                return UiStrings.Get("TrayTooltipHotkeyDisabled");

            if (_hotkeyService.IsRegistered)
                return UiStrings.Format("TrayTooltipHotkey", _settings.Hotkey.ToDisplayString());

            return UiStrings.Get("TrayTooltipHotkeyUnavailable");
        }

        private void SettingsForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            ExitThread();
        }

        private void SettingsForm_RestartRequested()
        {
            ShouldRestart = true;
            ExitApplication();
        }

        /// <summary>
        /// Przywraca formularz ukryty po minimalizacji i przekazuje mu fokus.
        /// </summary>
        private void ShowSettingsForm()
        {
            if (_settingsForm.IsDisposed)
                return;

            _settingsForm.Show();
            _settingsForm.WindowState = FormWindowState.Normal;
            _settingsForm.Activate();
        }

        private void ExitApplication()
        {
            if (!_settingsForm.IsDisposed)
            {
                _settingsForm.Close();
                return;
            }

            ExitThread();
        }

        /// <summary>
        /// Releases tray resources before leaving the message loop so Windows does not
        /// leave a visible icon after the process exits.
        /// </summary>
        protected override void ExitThreadCore()
        {
            _hotkeyService.Dispose();
            _dictationController.Dispose();
            DisposeTrayResources();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hotkeyService.Dispose();
                _dictationController.Dispose();
                DisposeTrayResources();
            }

            base.Dispose(disposing);
        }

        private void DisposeTrayResources()
        {
            if (_trayResourcesDisposed)
                return;

            _trayResourcesDisposed = true;
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _idleIcon.Dispose();
            _recordingIcon.Dispose();
            _transcribingIcon.Dispose();
            _trayMenu.Dispose();
        }
    }
}
