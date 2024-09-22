using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public class DeviceDto : EntityDtoBase
{
  [MsgPackKey]
  [Display(Name = "Agent Version")]
  public string AgentVersion { get; init; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "Alias")]
  public string Alias { get; init; } = string.Empty;

  [MsgPackKey]
  public string ConnectionId { get; set; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "CPU Utilization")]
  public double CpuUtilization { get; init; }

  [MsgPackKey]
  [Display(Name = "Current Users")]
  public string[] CurrentUsers { get; init; } = [];

  [MsgPackKey]
  [Display(Name = "Drives")]
  public List<Drive> Drives { get; init; } = [];

  [MsgPackKey]
  [Display(Name = "Device Id")]
  public int DeviceId { get; init; }

  [MsgPackKey]
  [Display(Name = "64-bit")]
  public bool Is64Bit { get; init; }

  [MsgPackKey]
  [Display(Name = "Online")]
  public bool IsOnline { get; set; }

  [MsgPackKey]
  [Display(Name = "Last Seen")]
  public DateTimeOffset LastSeen { get; set; }

  [MsgPackKey]
  [Display(Name = "MAC Addresses")]
  public string[] MacAddresses { get; init; } = [];

  [MsgPackKey]
  [Display(Name = "Device Name")]
  public string Name { get; init; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "OS Architecture")]
  public Architecture OsArchitecture { get; init; }

  [MsgPackKey]
  [Display(Name = "OS Description")]
  public string OsDescription { get; init; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "Platform")]
  public SystemPlatform Platform { get; init; }

  [MsgPackKey]
  [Display(Name = "Processor Count")]
  public int ProcessorCount { get; init; }

  [MsgPackKey]
  [Display(Name = "Public IP (v4)")]
  public string PublicIpV4 { get; init; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "Public IP (v6)")]
  public string PublicIpV6 { get; init; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "Memory Total")]
  public double TotalMemory { get; init; }

  [MsgPackKey]
  [Display(Name = "Storage Total")]
  public double TotalStorage { get; init; }

  [MsgPackKey]
  [Display(Name = "Memory Used")]
  public double UsedMemory { get; init; }

  [IgnoreDataMember]
  [JsonIgnore]
  [Display(Name = "Memory Used %")]
  public double UsedMemoryPercent => UsedMemory / TotalMemory;

  [MsgPackKey]
  [Display(Name = "Storage Used")]
  public double UsedStorage { get; init; }

  [IgnoreDataMember]
  [JsonIgnore]
  [Display(Name = "Storage Used %")]
  public double UsedStoragePercent => UsedStorage / TotalStorage;
}