using ControlR.Libraries.Shared.Serialization;
using MessagePack;

namespace ControlR.Web.Client.Models;

[MessagePackObject]
public class SettingsExport
{
    [MsgPackKey]
    public required DeviceDto[] Devices { get; init; }

    [MsgPackKey]
    public required bool HideOfflineDevices { get; init; }

    [MsgPackKey]
    public required bool NotifyUserSessionStart { get; init; }


    [MsgPackKey]
    public required Uri ServerUri { get; init; }

    [MsgPackKey]
    public required string Username { get; init; }
}
