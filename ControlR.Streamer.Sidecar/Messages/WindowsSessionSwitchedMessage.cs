namespace ControlR.Streamer.Sidecar.Messages;

public record WindowsSessionSwitchedMessage(SessionSwitchReasonEx Reason, int SessionId);