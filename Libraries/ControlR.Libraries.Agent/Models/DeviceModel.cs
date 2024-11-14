using ControlR.Libraries.Shared.Dtos.Interfaces;
using ControlR.Libraries.Shared.Serialization;
using MessagePack;
using System.Runtime.InteropServices;

namespace ControlR.Libraries.Agent.Models;

public class DeviceModel
{
  public string AgentVersion { get; set; } = string.Empty;

  public string ConnectionId { get; set; } = string.Empty;

  public double CpuUtilization { get; set; }

  public string[] CurrentUsers { get; set; } = [];

  public IReadOnlyList<Drive> Drives { get; set; } = [];

  public Guid Id { get; set; }

  public bool Is64Bit { get; set; }

  public bool IsOnline { get; set; }

  public DateTimeOffset LastSeen { get; set; }

  public string[] MacAddresses { get; set; } = [];

  public string Name { get; set; } = string.Empty;

  public Architecture OsArchitecture { get; set; }

  public string OsDescription { get; set; } = string.Empty;

  public SystemPlatform Platform { get; set; }

  public int ProcessorCount { get; set; }

  public string PublicIpV4 { get; set; } = string.Empty;

  public string PublicIpV6 { get; set; } = string.Empty;

  public Guid[]? TagIds { get; set; }

  public Guid TenantId { get; set; }

  public double TotalMemory { get; set; }

  public double TotalStorage { get; set; }

  public double UsedMemory { get; set; }

  public double UsedStorage { get; set; }

}