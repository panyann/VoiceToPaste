using System.Runtime.InteropServices;
using Serilog;

namespace VoiceToPaste.Services
{
    /// <summary>
    /// Przekazuje gotową transkrypcję do schowka, a następnie symuluje wklejenie do okna,
    /// które jest aktywne dokładnie w chwili zakończenia transkrypcji.
    /// </summary>
    internal sealed class TranscriptionOutput
    {
        private const int ClipboardWriteAttempts = 3;
        private const int ClipboardRetryDelayMilliseconds = 100;
        private const uint InputKeyboard = 1;
        private const ushort VirtualKeyControl = 0x11;
        private const ushort VirtualKeyV = 0x56;
        private const uint KeyEventKeyUp = 0x0002;

        private static readonly ILogger Logger = Log.ForContext<TranscriptionOutput>();

        internal static int InputStructureSize => Marshal.SizeOf<Input>();

        public async Task WriteAndPasteAsync(string text, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);

            for (var attempt = 1; attempt <= ClipboardWriteAttempts; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    break;
                }
                catch (ExternalException ex) when (attempt < ClipboardWriteAttempts)
                {
                    Logger.Warning(ex, "The clipboard is temporarily busy. Retrying write ({Attempt}/{Attempts}).",
                        attempt,
                        ClipboardWriteAttempts);
                    await Task.Delay(ClipboardRetryDelayMilliseconds, cancellationToken);
                }
            }

            var inputs = new[]
            {
                CreateKeyboardInput(VirtualKeyControl, 0),
                CreateKeyboardInput(VirtualKeyV, 0),
                CreateKeyboardInput(VirtualKeyV, KeyEventKeyUp),
                CreateKeyboardInput(VirtualKeyControl, KeyEventKeyUp),
            };
            var sentInputs = SendInput((uint)inputs.Length, inputs, InputStructureSize);
            if (sentInputs != (uint)inputs.Length)
            {
                var errorCode = Marshal.GetLastWin32Error();
                throw new PasteFailedException(errorCode);
            }

            Logger.Information("Saved the transcription to the clipboard and sent paste input to the active window.");
        }

        private static Input CreateKeyboardInput(ushort virtualKey, uint flags) => new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = flags,
                },
            },
        };

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KeyboardInput Keyboard;

            // INPUT ma unię z MOUSEINPUT, KEYBDINPUT i HARDWAREINPUT. Nawet gdy wysyłamy
            // wyłącznie klawisze, największy wariant wyznacza wymagany przez SendInput rozmiar.
            [FieldOffset(0)]
            public MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }
    }

    internal sealed class PasteFailedException : Exception
    {
        public PasteFailedException(int errorCode)
            : base($"Failed to paste the transcription (Windows error: {errorCode}).")
        {
        }
    }
}
