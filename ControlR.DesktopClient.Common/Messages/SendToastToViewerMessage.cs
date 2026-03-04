using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.DesktopClient.Common.Messages;

public sealed record SendToastToViewerMessage(string Message, MessageSeverity Severity);