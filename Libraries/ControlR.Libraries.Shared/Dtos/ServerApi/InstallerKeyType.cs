using System.Runtime.Serialization;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[DataContract]
public enum InstallerKeyType
{
  [EnumMember]
  Unknown = 0,
  [EnumMember]
  SingleUse = 1,
  [EnumMember]
  AbsoluteExpiration = 2
}
