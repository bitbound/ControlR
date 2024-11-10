using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using System.Runtime.InteropServices;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject]
public class DeviceUpdateRequestDto
{
  [MsgPackKey]
  public string AgentVersion { get; set; } = string.Empty;

  [MsgPackKey]
  public string ConnectionId { get; set; } = string.Empty;

  [MsgPackKey]
  public double CpuUtilization { get; set; }

  [MsgPackKey]
  public string[] CurrentUsers { get; set; } = [];

  [MsgPackKey]
  public List<Drive> Drives { get; set; } = [];

  [MsgPackKey]
  public Guid Id { get; set; }

  [MsgPackKey]
  public bool Is64Bit { get; set; }

  [MsgPackKey]
  public bool IsOnline { get; set; }

  [MsgPackKey]
  public DateTimeOffset LastSeen { get; set; }

  [MsgPackKey]
  public string[] MacAddresses { get; set; } = [];

  [MsgPackKey]
  public string Name { get; set; } = string.Empty;

  [MsgPackKey]
  public Architecture OsArchitecture { get; set; }

  [MsgPackKey]
  public string OsDescription { get; set; } = string.Empty;

  [MsgPackKey]
  public SystemPlatform Platform { get; set; }

  [MsgPackKey]
  public int ProcessorCount { get; set; }

  [MsgPackKey]
  public string PublicIpV4 { get; set; } = string.Empty;

  [MsgPackKey]
  public string PublicIpV6 { get; set; } = string.Empty;

  [MsgPackKey]
  public Guid[]? TagIds { get; set; }

  [MsgPackKey]
  public Guid TenantId { get; set; }

  [MsgPackKey]
  public double TotalMemory { get; set; }

  [MsgPackKey]
  public double TotalStorage { get; set; }

  [MsgPackKey]
  public double UsedMemory { get; set; }

  [MsgPackKey]
  public double UsedStorage { get; set; }
}