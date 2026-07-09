using System.Runtime.Serialization;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

[DataContract]
public enum CreatorKind
{
  [EnumMember]
  User = 0,
  [EnumMember]
  ServerServiceAccount = 1,
  [EnumMember]
  TenantServiceAccount = 2
}
