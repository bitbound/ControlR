using System.Runtime.Serialization;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[DataContract]
public record CreateInstallerKeyRequestDto(
  [property: DataMember] InstallerKeyType KeyType,
  [property: DataMember] DateTimeOffset? Expiration = null);
