using System.Runtime.Serialization;

namespace ControlR.Shared.Enums;

public enum AlertSeverity
{
    [EnumMember]
    Information,

    [EnumMember]
    Warning,

    [EnumMember]
    Error
}