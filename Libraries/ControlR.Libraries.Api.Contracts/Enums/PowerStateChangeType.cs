using System.Runtime.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

public enum PowerStateChangeType
{
  [EnumMember]
  None,

  [EnumMember]
  Restart,

  [EnumMember]
  Shutdown
}
