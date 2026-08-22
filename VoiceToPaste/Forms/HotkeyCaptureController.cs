using VoiceToPaste.Models;
using VoiceToPaste.Resources;
using System.Runtime.InteropServices;

namespace VoiceToPaste.Forms
{
    /// <summary>
    /// Zamienia zwykły TextBox tylko do odczytu w pole przechwytujące kombinację klawiszy.
    /// Kontrolka nie zapisuje ustawień ani nie rejestruje skrótu — przekazuje wyłącznie wybór UI.
    /// </summary>
    internal sealed class HotkeyCaptureController : IDisposable
    {
        private readonly TextBox _textBox;
        private HotkeyGesture? _displayedHotkey;
        private bool _isCapturing;

        public HotkeyCaptureController(TextBox textBox)
        {
            _textBox = textBox;
            _textBox.ReadOnly = true;
            _textBox.ShortcutsEnabled = false;
            _textBox.Enter += TextBox_Enter;
            _textBox.MouseDown += TextBox_MouseDown;
            _textBox.KeyDown += TextBox_KeyDown;
            _textBox.KeyPress += TextBox_KeyPress;
        }

        public event Action<HotkeyGesture?>? GestureCaptured;
        public event Action<bool>? CaptureStateChanged;

        public void SetHotkey(HotkeyGesture? hotkey)
        {
            _displayedHotkey = hotkey;
            _textBox.Text = hotkey?.ToDisplayString() ?? UiStrings.Get("HotkeyDisabled");
            EndCapture();
        }

        private void TextBox_Enter(object? sender, EventArgs e)
        {
            BeginCapture();
        }

        private void TextBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                BeginCapture();
        }

        private void TextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;

            if (!_isCapturing)
                BeginCapture();

            if (e.KeyCode == Keys.Escape)
            {
                SetHotkey(_displayedHotkey);
                return;
            }

            if (e.KeyCode is Keys.Back or Keys.Delete)
            {
                GestureCaptured?.Invoke(null);
                return;
            }

            if (IsModifierKey(e.KeyCode))
                return;

            if (!TryCreateGesture(e, out var hotkey))
            {
                _textBox.Text = UiStrings.Get("HotkeyUnsupported");
                return;
            }

            GestureCaptured?.Invoke(hotkey);
        }

        private void TextBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            e.Handled = true;
        }

        private void BeginCapture()
        {
            if (_isCapturing)
                return;

            _isCapturing = true;
            _textBox.Text = UiStrings.Get("HotkeyCapturePrompt");
            CaptureStateChanged?.Invoke(true);
        }

        private void EndCapture()
        {
            if (!_isCapturing)
                return;

            _isCapturing = false;
            CaptureStateChanged?.Invoke(false);
        }

        private static bool TryCreateGesture(KeyEventArgs e, out HotkeyGesture hotkey)
        {
            if (!TryGetKeyName(e.KeyCode, out var key))
            {
                hotkey = null!;
                return false;
            }

            var modifiers = new List<HotkeyModifier>();
            if (e.Control)
                modifiers.Add(HotkeyModifier.Control);
            if (e.Shift)
                modifiers.Add(HotkeyModifier.Shift);
            if (e.Alt)
                modifiers.Add(HotkeyModifier.Alt);
            if (IsWindowsKeyPressed())
                modifiers.Add(HotkeyModifier.Windows);

            hotkey = new HotkeyGesture { Key = key, Modifiers = modifiers };
            return hotkey.IsValid(out _);
        }

        private static bool TryGetKeyName(Keys keyCode, out string key)
        {
            if (keyCode == Keys.Space)
            {
                key = "Space";
                return true;
            }

            if (keyCode is >= Keys.A and <= Keys.Z)
            {
                key = keyCode.ToString();
                return true;
            }

            if (keyCode is >= Keys.D0 and <= Keys.D9)
            {
                key = ((int)keyCode - (int)Keys.D0).ToString();
                return true;
            }

            if (keyCode is >= Keys.F1 and <= Keys.F24)
            {
                key = keyCode.ToString();
                return true;
            }

            key = string.Empty;
            return false;
        }

        private static bool IsModifierKey(Keys keyCode) => keyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin;

        private static bool IsWindowsKeyPressed() =>
            (GetAsyncKeyState((int)Keys.LWin) & 0x8000) != 0 ||
            (GetAsyncKeyState((int)Keys.RWin) & 0x8000) != 0;

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        public void Dispose()
        {
            _textBox.Enter -= TextBox_Enter;
            _textBox.MouseDown -= TextBox_MouseDown;
            _textBox.KeyDown -= TextBox_KeyDown;
            _textBox.KeyPress -= TextBox_KeyPress;
        }
    }
}
