namespace ControlR.Web.Server.Extensions;

public static class EntityToDtoExtensions
{
  public static DeviceDto ToDto(this Device device)
  {
    return new DeviceDto()
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
      TenantId = device.TenantId ?? 0,
      TotalMemory = device.TotalMemory,
      TotalStorage = device.TotalStorage,
      UsedMemory = device.UsedMemory,
      UsedStorage = device.UsedStorage,
      Uid = device.Uid
    };
  }
}
