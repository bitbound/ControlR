using System.Runtime.Serialization;

namespace ControlR.Libraries.Shared.Enums;

public enum AlertSeverity
{
    [EnumMember]
    Information,

    [EnumMember]
    Warning,

    [EnumMember]
    Error
}