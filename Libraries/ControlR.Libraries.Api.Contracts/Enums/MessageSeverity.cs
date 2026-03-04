using System.Runtime.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

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
