using System.Collections.Immutable;

namespace ControlR.Web.Server.Extensions;

public static class EntityToDtoExtensions
{
  public static CreateInstallerKeyResponseDto ToCreateResponseDto(this AgentInstallerKey key, string plaintextKey)
  {
    return new CreateInstallerKeyResponseDto(
      key.Id,
      key.CreatorId,
      key.KeyType,
      plaintextKey,
      key.CreatedAt,
      key.AllowedUses,
      key.Expiration,
      key.FriendlyName);
  }
  public static DeviceResponseDto ToDto(this Device device, bool isOutdated)
  {
    return new DeviceResponseDto(
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
      isOutdated)
    {
      Alias = device.Alias,
      TagIds = device.Tags?.Select(x => x.Id).ToImmutableArray()
    };
  }
  public static RoleResponseDto ToDto(this AppRole role)
  {
    var userIds = role
      .UserRoles
      ?.Select(x => x.UserId)
      ?.ToList() ?? [];

    return new RoleResponseDto(
      role.Id,
      role.Name ?? string.Empty,
      userIds);
  }
  public static UserPreferenceResponseDto ToDto(this UserPreference userPreference)
  {
    return new UserPreferenceResponseDto(userPreference.Id, userPreference.Name, userPreference.Value);
  }
  public static ServerAlertResponseDto ToDto(this ServerAlert serverAlert)
  {
    return new ServerAlertResponseDto(
      serverAlert.Id,
      serverAlert.Message,
      serverAlert.Severity,
      serverAlert.IsDismissable,
      serverAlert.IsSticky,
      serverAlert.IsEnabled);
  }
  public static TenantSettingResponseDto ToDto(this TenantSetting tenantSetting)
  {
    return new TenantSettingResponseDto(tenantSetting.Id, tenantSetting.Name, tenantSetting.Value);
  }
  public static TagResponseDto ToDto(this Tag tag)
  {
    var userIds = tag
      .Users?
      .Select(x => x.Id)
      .ToList() ?? [];

    var deviceIds = tag
      .Devices?
      .Select(x => x.Id)
      .ToList() ?? [];

    return new TagResponseDto(
      tag.Id,
      tag.Name,
      tag.Type,
      userIds,
      deviceIds);
  }
  public static AgentInstallerKeyDto ToDto(this AgentInstallerKey key)
  {
    return new AgentInstallerKeyDto(
      key.Id,
      key.CreatorId,
      key.KeyType,
      key.CreatedAt,
      key.AllowedUses,
      key.Expiration,
      key.FriendlyName,
      key.Usages?.Select(u => new AgentInstallerKeyUsageDto(u.Id, u.DeviceId, u.CreatedAt, u.RemoteIpAddress)).ToList());
  }
}
