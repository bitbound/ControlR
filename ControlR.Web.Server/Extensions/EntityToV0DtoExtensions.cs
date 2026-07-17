using System.Collections.Immutable;

namespace ControlR.Web.Server.Extensions;

public static class EntityToV0DtoExtensions
{
  public static V0Dtos.CreateInstallerKeyResponseDto ToV0ResponseDto(this AgentInstallerKey key, string plaintextKey)
  {
    return new V0Dtos.CreateInstallerKeyResponseDto(
      key.Id,
      key.CreatorId,
      key.KeyType,
      plaintextKey,
      key.CreatedAt,
      key.AllowedUses,
      key.Expiration,
      key.FriendlyName);
  }

  public static V0Dtos.DeviceResponseDto ToV0ResponseDto(this Device device, bool isOutdated)
  {
    return new V0Dtos.DeviceResponseDto(
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

  public static V0Dtos.DeviceSummaryDto ToV0SummaryDto(this Device device)
  {
    return new V0Dtos.DeviceSummaryDto(
      device.Id,
      device.LastSeen,
      device.AgentVersion);
  }
}
