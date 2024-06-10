using System.Runtime.Serialization;

namespace ControlR.Libraries.Shared.Enums;

public enum PowerStateChangeType
{
    [EnumMember]
    None,

    [EnumMember]
    Restart,

    [EnumMember]
    Shutdown
}
