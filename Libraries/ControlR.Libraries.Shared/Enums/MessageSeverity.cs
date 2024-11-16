using System.Runtime.Serialization;

namespace ControlR.Libraries.Shared.Enums;

public enum MessageSeverity
{
  [EnumMember]
  Information,
  [EnumMember]
  Warning,
  [EnumMember]
  Error,
  [EnumMember]
  Success,
}
