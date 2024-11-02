using System.Collections.Immutable;

namespace ControlR.Web.Server.Extensions;

public static class EntityToDtoExtensions
{
  public static DeviceResponseDto ToDto(this Device device)
  {
    return new DeviceResponseDto()
    {
      AgentVersion = device.AgentVersion,
      Alias = device.Alias,
      ConnectionId = device.ConnectionId,
      CpuUtilization = device.CpuUtilization,
      CurrentUsers = device.CurrentUsers,
      DeviceGroupId = device.DeviceGroupId,
      Drives = device.Drives,
      Id = device.Id,
      Is64Bit = device.Is64Bit,
      IsOnline = device.IsOnline,
      LastSeen  = device.LastSeen,
      MacAddresses = device.MacAddresses,
      Name = device.Name,
      OsArchitecture = device.OsArchitecture,
      OsDescription = device.OsDescription,
      Platform = device.Platform,
      ProcessorCount = device.ProcessorCount,
      PublicIpV4 = device.PublicIpV4,
      PublicIpV6 = device.PublicIpV6,
      TenantId = device.TenantId,
      TotalMemory = device.TotalMemory,
      TotalStorage = device.TotalStorage,
      UsedMemory = device.UsedMemory,
      UsedStorage = device.UsedStorage,
    };
  }

  public static UserPreferenceResponseDto ToDto(this UserPreference userPreference)
  {
    return new UserPreferenceResponseDto(userPreference.Id, userPreference.Name, userPreference.Value);
  }

  public static TagResponseDto ToDto(this Tag tag)
  {
    var userTuples = tag
      .Users?
      .Select(x => new IdNameTuple(x.Id, x.UserName ?? ""))
      .ToImmutableArray() ?? [];
    
    var deviceTuples = tag
      .Devices?
      .Select(x => new IdNameTuple(x.Id, x.Name))
      .ToImmutableArray() ?? [];
    
    return new TagResponseDto(
      tag.Id, 
      tag.Name, 
      tag.Type, 
      userTuples,
      deviceTuples);
  }
}
