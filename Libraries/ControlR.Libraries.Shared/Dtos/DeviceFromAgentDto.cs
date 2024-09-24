using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Models;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public class DeviceFromAgentDto : EntityDtoBase
{
  [MsgPackKey]
  [Display(Name = "Agent Version")]
  public string AgentVersion { get; set; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "CPU Utilization")]
  public double CpuUtilization { get; set; }

  [MsgPackKey]
  [Display(Name = "Current Users")]
  public string[] CurrentUsers { get; set; } = [];

  [MsgPackKey]
  [Display(Name = "Drives")]
  public List<Drive> Drives { get; set; } = [];

  [MsgPackKey]
  [Display(Name = "64-bit")]
  public bool Is64Bit { get; set; }

  [MsgPackKey]
  [Display(Name = "Online")]
  public bool IsOnline { get; set; }

  [MsgPackKey]
  [Display(Name = "Last Seen")]
  public DateTimeOffset LastSeen { get; set; }

  [MsgPackKey]
  [Display(Name = "MAC Addresses")]
  public string[] MacAddresses { get; set; } = [];

  [MsgPackKey]
  [Display(Name = "Device Name")]
  public string Name { get; set; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "OS Architecture")]
  public Architecture OsArchitecture { get; set; }

  [MsgPackKey]
  [Display(Name = "OS Description")]
  public string OsDescription { get; set; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "Platform")]
  public SystemPlatform Platform { get; set; }

  [MsgPackKey]
  [Display(Name = "Processor Count")]
  public int ProcessorCount { get; set; }

  [MsgPackKey]
  [Display(Name = "Public IP (v4)")]
  public string PublicIpV4 { get; set; } = string.Empty;

  [MsgPackKey]
  [Display(Name = "Public IP (v6)")]
  public string PublicIpV6 { get; set; } = string.Empty;
  [MsgPackKey]
  [Display(Name = "Memory Total")]
  public double TotalMemory { get; set; }

  [MsgPackKey]
  [Display(Name = "Storage Total")]
  public double TotalStorage { get; set; }

  [MsgPackKey]
  [Display(Name = "Memory Used")]
  public double UsedMemory { get; set; }

  [MsgPackKey]
  [Display(Name = "Storage Used")]
  public double UsedStorage { get; set; }
}