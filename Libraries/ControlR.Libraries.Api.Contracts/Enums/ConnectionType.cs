using System.Runtime.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

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
