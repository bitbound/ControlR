using System.Runtime.Serialization;

namespace ControlR.Shared.Enums;

public enum PowerStateChangeType
{
    [EnumMember]
    None,

    [EnumMember]
    Restart,

    [EnumMember]
    Shutdown
}
