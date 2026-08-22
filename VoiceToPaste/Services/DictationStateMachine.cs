namespace VoiceToPaste.Services
{
    internal enum DictationState
    {
        Idle,
        Recording,
        Transcribing,
    }

    /// <summary>
    /// Pilnuje dozwolonych przejść stanu dyktowania. Nie wykonuje operacji audio ani Whispera,
    /// dzięki czemu reguły przejść można testować niezależnie od urządzeń systemowych.
    /// </summary>
    internal sealed class DictationStateMachine
    {
        public DictationState State { get; private set; } = DictationState.Idle;

        public bool TryStartRecording()
        {
            if (State != DictationState.Idle)
                return false;

            State = DictationState.Recording;
            return true;
        }

        public bool TryStartTranscribing()
        {
            if (State != DictationState.Recording)
                return false;

            State = DictationState.Transcribing;
            return true;
        }

        public bool TryReturnToIdle()
        {
            if (State == DictationState.Idle)
                return false;

            State = DictationState.Idle;
            return true;
        }
    }
}
