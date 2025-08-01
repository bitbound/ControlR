using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.InteropServices;
using ControlR.Libraries.Shared.Enums;
using ControlR.Web.Server.Data.Entities.Bases;

namespace ControlR.Web.Server.Data.Entities;

public class Device : TenantEntityBase
{
  [StringLength(50)]
  public string AgentVersion { get; set; } = string.Empty;

  [StringLength(100)]
  public string Alias { get; set; } = string.Empty;
  public string ConnectionId { get; set; } = string.Empty;
  public double CpuUtilization { get; set; }

  public string[] CurrentUsers { get; set; } = [];
  public Guid? DeviceGroupId { get; set; }

  public List<Drive> Drives { get; set; } = [];
  public bool Is64Bit { get; set; }
  public bool IsOnline { get; set; }
  public DateTimeOffset LastSeen { get; set; }
  public string[] MacAddresses { get; set; } = [];

  [StringLength(100)]
  public string Name { get; set; } = string.Empty;

  public Architecture OsArchitecture { get; set; }

  [StringLength(300)]
  public string OsDescription { get; set; } = string.Empty;

  public SystemPlatform Platform { get; set; }
  public int ProcessorCount { get; set; }

  [StringLength(15)]
  public string PublicIpV4 { get; set; } = string.Empty;

  [StringLength(39)]
  public string PublicIpV6 { get; set; } = string.Empty;

  public List<Tag>? Tags { get; set; }

  public double TotalMemory { get; set; }
  public double TotalStorage { get; set; }

  public double UsedMemory { get; set; }
  [NotMapped]
  public double UsedMemoryPercent => UsedMemory / TotalMemory;

  public double UsedStorage { get; set; }
  [NotMapped]
  public double UsedStoragePercent => UsedStorage / TotalStorage;
}