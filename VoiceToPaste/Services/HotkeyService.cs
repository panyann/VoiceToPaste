using System.Runtime.InteropServices;
using Serilog;
using VoiceToPaste.Models;

namespace VoiceToPaste.Services
{
    /// <summary>
    /// Rejestruje globalny skrót Windows i przekazuje jego aktywację bez znajomości
    /// logiki nagrywania ani transkrypcji.
    /// </summary>
    internal sealed class HotkeyService : NativeWindow, IDisposable
    {
        private const int FirstHotkeyId = 1;
        private const int SecondHotkeyId = 2;
        private const uint WmHotkey = 0x0312;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModAlt = 0x0001;
        private const uint ModWindows = 0x0008;
        private const uint ModNoRepeat = 0x4000;

        private static readonly ILogger Logger = Log.ForContext<HotkeyService>();
        private bool _isRegistered;
        private bool _isDisposed;
        private int _registeredHotkeyId;
        private uint _registeredModifiers;
        private uint _registeredVirtualKey;

        public event EventHandler? Activated;

        public bool IsRegistered => _isRegistered;

        /// <summary>
        /// Próbuje zarejestrować skrót z ustawień.
        /// Własne niewidoczne okno zapewnia odbieranie komunikatu niezależnie od
        /// widoczności formularza ustawień.
        /// </summary>
        public bool TryUpdateRegistration(HotkeyGesture? hotkey, out int errorCode)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            uint modifiers = 0;
            uint virtualKey = 0;
            if (hotkey == null)
            {
                return TryUnregisterCurrent(out errorCode);
            }

            if (!hotkey.IsValid(out _))
            {
                errorCode = 87;
                Logger.Warning("Cannot register an invalid hotkey.");
                return false;
            }

            modifiers = GetModifierFlags(hotkey) | ModNoRepeat;
            hotkey.TryGetVirtualKey(out virtualKey);
            if (_isRegistered && _registeredModifiers == modifiers && _registeredVirtualKey == virtualKey)
            {
                errorCode = 0;
                return true;
            }

            if (Handle == IntPtr.Zero)
                CreateHandle(new CreateParams());

            if (!_isRegistered)
            {
                return TryRegisterNew(FirstHotkeyId, modifiers, virtualKey, hotkey.ToDisplayString(), out errorCode);
            }

            // Drugi identyfikator pozwala najpierw zarezerwować nową kombinację. Gdy rejestracja
            // się nie powiedzie, poprzedni skrót nadal działa i użytkownik nie traci aktywacji.
            var newHotkeyId = _registeredHotkeyId == FirstHotkeyId ? SecondHotkeyId : FirstHotkeyId;
            if (!RegisterHotKey(Handle, newHotkeyId, modifiers, virtualKey))
            {
                errorCode = Marshal.GetLastWin32Error();
                Logger.Warning("Failed to register new hotkey {Hotkey}. Win32 error code: {ErrorCode}.",
                    hotkey.ToDisplayString(),
                    errorCode);
                return false;
            }

            if (!UnregisterHotKey(Handle, _registeredHotkeyId))
            {
                errorCode = Marshal.GetLastWin32Error();
                UnregisterHotKey(Handle, newHotkeyId);
                Logger.Error("Failed to unregister the previous hotkey. Win32 error code: {ErrorCode}.", errorCode);
                return false;
            }

            _registeredHotkeyId = newHotkeyId;
            _registeredModifiers = modifiers;
            _registeredVirtualKey = virtualKey;
            errorCode = 0;
            Logger.Information("Changed the global hotkey to {Hotkey}.", hotkey.ToDisplayString());
            return true;
        }

        private bool TryRegisterNew(int hotkeyId, uint modifiers, uint virtualKey, string displayText, out int errorCode)
        {
            if (!RegisterHotKey(Handle, hotkeyId, modifiers, virtualKey))
            {
                errorCode = Marshal.GetLastWin32Error();
                Logger.Warning("Failed to register global hotkey {Hotkey}. Win32 error code: {ErrorCode}.",
                    displayText,
                    errorCode);
                return false;
            }

            _isRegistered = true;
            _registeredHotkeyId = hotkeyId;
            _registeredModifiers = modifiers;
            _registeredVirtualKey = virtualKey;
            errorCode = 0;
            Logger.Information("Registered global hotkey {Hotkey}.", displayText);
            return true;
        }

        private bool TryUnregisterCurrent(out int errorCode)
        {
            if (!_isRegistered)
            {
                errorCode = 0;
                return true;
            }

            if (!UnregisterHotKey(Handle, _registeredHotkeyId))
            {
                errorCode = Marshal.GetLastWin32Error();
                Logger.Warning("Failed to unregister the global hotkey. Win32 error code: {ErrorCode}.", errorCode);
                return false;
            }

            _isRegistered = false;
            _registeredHotkeyId = 0;
            _registeredModifiers = 0;
            _registeredVirtualKey = 0;
            errorCode = 0;
            Logger.Information("Unregistered the global hotkey.");
            return true;
        }

        private static uint GetModifierFlags(HotkeyGesture hotkey)
        {
            var flags = 0u;
            foreach (var modifier in hotkey.Modifiers)
            {
                flags |= modifier switch
                {
                    HotkeyModifier.Control => ModControl,
                    HotkeyModifier.Shift => ModShift,
                    HotkeyModifier.Alt => ModAlt,
                    HotkeyModifier.Windows => ModWindows,
                    _ => 0,
                };
            }

            return flags;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam == (nint)_registeredHotkeyId)
            {
                Logger.Information("Received global hotkey activation.");
                Activated?.Invoke(this, EventArgs.Empty);
                return;
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            TryUnregisterCurrent(out _);

            if (Handle != IntPtr.Zero)
                ReleaseHandle();

            GC.SuppressFinalize(this);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
