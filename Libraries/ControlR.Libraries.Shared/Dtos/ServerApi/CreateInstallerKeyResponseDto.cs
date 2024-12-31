using System.Runtime.Serialization;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[DataContract]
public record CreateInstallerKeyResponseDto(
  [property: DataMember] InstallerKeyType TokenType,
  [property: DataMember] string AccessKey,
  [property: DataMember] uint? AllowedUses = null,
  [property: DataMember] DateTimeOffset? Expiration = null);