using ControlR.Libraries.Shared.Enums;

namespace ControlR.DesktopClient.Common.Messages;

public sealed record SendToastToViewerMessage(string Message, MessageSeverity Severity);