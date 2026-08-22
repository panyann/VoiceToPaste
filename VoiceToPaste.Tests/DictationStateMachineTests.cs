using VoiceToPaste.Services;

namespace VoiceToPaste.Tests
{
    public sealed class DictationStateMachineTests
    {
        [Fact]
        public void TryStartRecording_FromIdle_ChangesStateToRecording()
        {
            var stateMachine = new DictationStateMachine();

            var changed = stateMachine.TryStartRecording();

            Assert.True(changed);
            Assert.Equal(DictationState.Recording, stateMachine.State);
        }

        [Fact]
        public void TryStartRecording_FromRecording_LeavesStateUnchanged()
        {
            var stateMachine = new DictationStateMachine();
            stateMachine.TryStartRecording();

            var changed = stateMachine.TryStartRecording();

            Assert.False(changed);
            Assert.Equal(DictationState.Recording, stateMachine.State);
        }

        [Fact]
        public void TryStartTranscribing_FromRecording_ChangesStateToTranscribing()
        {
            var stateMachine = new DictationStateMachine();
            stateMachine.TryStartRecording();

            var changed = stateMachine.TryStartTranscribing();

            Assert.True(changed);
            Assert.Equal(DictationState.Transcribing, stateMachine.State);
        }

        [Fact]
        public void TryStartTranscribing_FromIdle_LeavesStateUnchanged()
        {
            var stateMachine = new DictationStateMachine();

            var changed = stateMachine.TryStartTranscribing();

            Assert.False(changed);
            Assert.Equal(DictationState.Idle, stateMachine.State);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TryReturnToIdle_FromActiveState_ChangesStateToIdle(bool isTranscribing)
        {
            var stateMachine = new DictationStateMachine();
            stateMachine.TryStartRecording();
            if (isTranscribing)
                stateMachine.TryStartTranscribing();

            var changed = stateMachine.TryReturnToIdle();

            Assert.True(changed);
            Assert.Equal(DictationState.Idle, stateMachine.State);
        }

        [Fact]
        public void TryReturnToIdle_FromIdle_LeavesStateUnchanged()
        {
            var stateMachine = new DictationStateMachine();

            var changed = stateMachine.TryReturnToIdle();

            Assert.False(changed);
            Assert.Equal(DictationState.Idle, stateMachine.State);
        }
    }
}
