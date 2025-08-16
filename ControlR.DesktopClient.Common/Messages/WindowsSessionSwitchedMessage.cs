namespace ControlR.DesktopClient.Common.Messages;

public record WindowsSessionSwitchedMessage(SessionSwitchReasonEx Reason, int SessionId);