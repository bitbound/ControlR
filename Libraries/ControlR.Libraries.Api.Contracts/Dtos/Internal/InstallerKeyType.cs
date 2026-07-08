using System.Runtime.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

[DataContract]
public enum InstallerKeyType
{
  [EnumMember]
  Unknown = 0,
  [EnumMember]
  UsageBased = 1,
  [EnumMember]
  TimeBased = 2,
  [EnumMember]
  Persistent = 3
}
