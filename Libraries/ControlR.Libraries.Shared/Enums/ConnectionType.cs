using System.Runtime.Serialization;

namespace ControlR.Libraries.Shared.Enums;

public enum ConnectionType
{
    [EnumMember]
    Unknown,
    [EnumMember]
    Viewer,
    [EnumMember]
    Agent,
    [EnumMember]
    Desktop
}
