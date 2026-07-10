using System.Collections.Immutable;

namespace ControlR.Web.Server.Extensions;

public static class EntityToInternalDtoExtensions
{
  public static InternalDtos.CreateInstallerKeyResponseDto ToInternalResponseDto(this AgentInstallerKey key, string plaintextKey)
  {
    return new InternalDtos.CreateInstallerKeyResponseDto(
      key.Id,
      key.CreatorId,
      key.KeyType,
      plaintextKey,
      key.CreatedAt,
      key.AllowedUses,
      key.Expiration,
      key.FriendlyName);
  }

  public static InternalDtos.DeviceResponseDto ToInternalResponseDto(this Device device, bool isOutdated)
  {
    return new InternalDtos.DeviceResponseDto(
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

  public static InternalDtos.RoleResponseDto ToInternalResponseDto(this AppRole role)
  {
    var userIds = role
      .UserRoles
      ?.Select(x => x.UserId)
      ?.ToList() ?? [];

    return new InternalDtos.RoleResponseDto(
      role.Id,
      role.Name ?? string.Empty,
      userIds);
  }

  public static InternalDtos.UserPreferenceResponseDto ToInternalResponseDto(this UserPreference userPreference)
  {
    return new InternalDtos.UserPreferenceResponseDto(userPreference.Id, userPreference.Name, userPreference.Value);
  }

  public static InternalDtos.ServerAlertResponseDto ToInternalResponseDto(this ServerAlert serverAlert)
  {
    return new InternalDtos.ServerAlertResponseDto(
      serverAlert.Id,
      serverAlert.Message,
      serverAlert.Severity,
      serverAlert.IsDismissable,
      serverAlert.IsSticky,
      serverAlert.IsEnabled);
  }

  public static InternalDtos.TenantSettingResponseDto ToInternalResponseDto(this TenantSetting tenantSetting)
  {
    return new InternalDtos.TenantSettingResponseDto(tenantSetting.Id, tenantSetting.Name, tenantSetting.Value);
  }

  public static InternalDtos.TagResponseDto ToInternalResponseDto(this Tag tag)
  {
    var userIds = tag
      .Users?
      .Select(x => x.Id)
      .ToList() ?? [];

    var deviceIds = tag
      .Devices?
      .Select(x => x.Id)
      .ToList() ?? [];

    return new InternalDtos.TagResponseDto(
      tag.Id,
      tag.Name,
      tag.Type,
      userIds,
      deviceIds);
  }

  public static InternalDtos.DeviceSummaryDto ToInternalSummaryDto(this Device device)
  {
    return new InternalDtos.DeviceSummaryDto(
      device.Id,
      device.LastSeen,
      device.AgentVersion);
  }
}
