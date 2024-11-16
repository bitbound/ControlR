using ControlR.Libraries.Shared.Enums;
namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public record ToastInfo(
  [property: MsgPackKey] string Message, 
  [property: MsgPackKey] MessageSeverity MessageSeverity);
