using ControlR.Shared.Enums;
using ControlR.Shared.Serialization;
using MessagePack;
namespace ControlR.Shared.Models;

[MessagePackObject]
public class ToastInfo
{
    [MsgPackKey]
    public string Message { get; set; } = string.Empty;

    [MsgPackKey]
    public MessageLevel MessageLevel { get; set; }
}
