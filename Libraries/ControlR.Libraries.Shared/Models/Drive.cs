using ControlR.Libraries.Shared.Serialization;
using MessagePack;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class Drive
{
    [MsgPackKey]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DriveType DriveType { get; set; }

    [MsgPackKey]
    public string RootDirectory { get; set; } = string.Empty;

    [MsgPackKey]
    public string Name { get; set; } = string.Empty;

    [MsgPackKey]
    public string DriveFormat { get; set; } = string.Empty;

    [MsgPackKey]
    public double FreeSpace { get; set; }

    [MsgPackKey]
    public double TotalSize { get; set; }

    [MsgPackKey]
    public string VolumeLabel { get; set; } = string.Empty;
}
