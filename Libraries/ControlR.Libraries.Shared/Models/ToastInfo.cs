using ControlR.Libraries.Shared.Enums;
namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class ToastInfo
{
    [MsgPackKey]
    public string Message { get; set; } = string.Empty;

    [MsgPackKey]
    public MessageLevel MessageLevel { get; set; }
}
