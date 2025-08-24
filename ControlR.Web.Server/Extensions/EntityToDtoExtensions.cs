using System.Collections.Immutable;

namespace ControlR.Web.Server.Extensions;

public static class EntityToDtoExtensions
{
  public static DeviceDto ToDto(this Device device)
  {
    return new DeviceDto(
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
      device.Drives)
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
}