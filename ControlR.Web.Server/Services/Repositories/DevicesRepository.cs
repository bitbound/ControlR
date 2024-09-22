using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;

namespace ControlR.Web.Server.Services.Repositories;

public class DevicesRepository(AppDb appDb) : RepositoryBase<DeviceDto, Device>(appDb)
{
  protected override DeviceDto MapToDto(Device entity)
  {
    return new DeviceDto
    {
      Alias = entity.Alias,
      MacAddresses = entity.MacAddresses,
      Name = entity.Name,
      PublicIpV4 = entity.PublicIpV4,
      Drives = entity.Drives,
      Platform = entity.Platform,
      Uid = entity.Uid,
      AgentVersion = entity.AgentVersion,
      CpuUtilization = entity.CpuUtilization,
      CurrentUsers = entity.CurrentUsers,
      Is64Bit = entity.Is64Bit,
      IsOnline = entity.IsOnline,
      LastSeen = entity.LastSeen,
      OsArchitecture = entity.OsArchitecture,
      OsDescription = entity.OsDescription,
      ProcessorCount = entity.ProcessorCount,
      TotalMemory = entity.TotalMemory,
      TotalStorage = entity.TotalStorage,
      UsedMemory = entity.UsedMemory,
      UsedStorage = entity.UsedStorage,
      DeviceId = entity.DeviceId,
      PublicIpV6 = entity.PublicIpV6,
    };
  }

  protected override Device MapToEntity(DeviceDto dto, Device? existing)
  {
    existing ??= new Device();

    existing.AgentVersion = dto.AgentVersion;
    existing.Alias = dto.Alias;
    existing.Name = dto.Name;
    existing.OsDescription = dto.OsDescription;
    existing.PublicIpV4 = dto.PublicIpV4;
    existing.PublicIpV6 = dto.PublicIpV6;
    existing.MacAddresses = dto.MacAddresses;
    existing.Drives = dto.Drives;
    existing.Platform = dto.Platform;
    existing.Uid = dto.Uid;
    existing.CpuUtilization = dto.CpuUtilization;
    existing.CurrentUsers = dto.CurrentUsers;
    existing.Is64Bit = dto.Is64Bit;
    existing.IsOnline = dto.IsOnline;
    existing.LastSeen = dto.LastSeen;
    existing.OsArchitecture = dto.OsArchitecture;
    existing.ProcessorCount = dto.ProcessorCount;
    existing.TotalMemory = dto.TotalMemory;
    existing.TotalStorage = dto.TotalStorage;
    existing.UsedMemory = dto.UsedMemory;
    existing.UsedStorage = dto.UsedStorage;
    existing.DeviceId = dto.DeviceId;

    return existing;
  }
}