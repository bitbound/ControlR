using System.Runtime.Serialization;

namespace ControlR.Streamer.Sidecar.IpcDtos;

[DataContract]
public enum SidecarDtoType
{
    [EnumMember(Value = nameof(Unknown))]
    Unknown,
    [EnumMember(Value = nameof(DesktopChanged))]
    DesktopChanged,
    [EnumMember(Value = nameof(DesktopRequest))]
    DesktopRequest
}
