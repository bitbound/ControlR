namespace ControlR.Streamer.Messages;

public record WindowsSessionSwitchedMessage(SessionSwitchReasonEx Reason, int SessionId);