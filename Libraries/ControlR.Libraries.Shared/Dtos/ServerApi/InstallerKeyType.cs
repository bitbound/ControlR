using System.Runtime.Serialization;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[DataContract]
public enum InstallerKeyType
{
  [EnumMember]
  Unknown = 0,
  [EnumMember]
  UsageBased = 1,
  [EnumMember]
  TimeBased = 2
}
