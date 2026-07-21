using System.Collections.Immutable;

namespace ControlR.Web.Server.Extensions;

public static class EntityToV1DtoExtensions
{
  public static V1Dtos.CreateInstallerKeyResponseDto ToV1ResponseDto(this AgentInstallerKey key, string plaintextKey)
  {
    return new V1Dtos.CreateInstallerKeyResponseDto(
      key.Id,
      key.CreatorId,
      key.KeyType,
      plaintextKey,
      key.CreatedAt,
      key.AllowedUses,
      key.Expiration,
      key.FriendlyName);
  }

  public static V1Dtos.DeviceResponseDto ToV1ResponseDto(this Device device, bool isOutdated)
  {
    return new V1Dtos.DeviceResponseDto(
      device.Name,
      device.AgentVersion,
      device.CpuUtilization,
      device.Id,
      device.Is64Bit,
      device.IsOnline,
      device.LastSeen,
      device.OsArchitecture,
      device.Platform,
      device.ProcessorCount,
      device.ConnectionId,
      device.OsDescription,
      device.TenantId,
      device.TotalMemory,
      device.TotalStorage,
      device.UsedMemory,
      device.UsedStorage,
      device.CurrentUsers,
      device.MacAddresses,
      device.PublicIpV4,
      device.PublicIpV6,
      device.LocalIpV4,
      device.LocalIpV6,
      device.Drives,
      isOutdated,
      device.DnsHostName)
    {
      Alias = device.Alias,
      TagIds = device.Tags?.Select(x => x.Id).ToImmutableArray()
    };
  }

  public static V1Dtos.DeviceSummaryDto ToV1SummaryDto(this Device device)
  {
    return new V1Dtos.DeviceSummaryDto(
      device.Id,
      device.LastSeen,
      device.AgentVersion);
  }
}
