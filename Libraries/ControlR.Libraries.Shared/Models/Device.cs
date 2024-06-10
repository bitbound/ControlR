using ControlR.Libraries.Shared.Enums;
using ControlR.Libraries.Shared.Serialization;
using MessagePack;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Models;

[MessagePackObject]
public class Device
{
    [MsgPackKey]
    [Display(Name = "Agent Version")]
    public string AgentVersion { get; set; } = string.Empty;

    [MsgPackKey]
    [StringLength(100)]
    [Display(Name = "Alias")]
    public string Alias { get; set; } = string.Empty;

    [MsgPackKey]
    [Display(Name = "Authorized Keys")]
    public IEnumerable<string> AuthorizedKeys { get; set; } = Array.Empty<string>();

    [MsgPackKey]
    [Display(Name = "CPU Utilization")]
    public double CpuUtilization { get; set; }

    [MsgPackKey]
    [Display(Name = "Current User")]
    public string CurrentUser { get; set; } = string.Empty;

    [MsgPackKey]
    [Display(Name = "Drives")]
    public List<Drive> Drives { get; set; } = [];

    [MsgPackKey]
    [Display(Name = "Device Id")]
    public string Id { get; set; } = string.Empty;

    [MsgPackKey]
    [Display(Name = "64-bit")]
    public bool Is64Bit { get; set; }

    [MsgPackKey]
    [Display(Name = "MAC Addresses")]
    public string[] MacAddresses { get; set; } = [];

    [MsgPackKey]
    [Display(Name = "Online")]
    public bool IsOnline { get; set; }

    [MsgPackKey]
    [Display(Name = "Last Seen")]
    public DateTimeOffset LastSeen { get; set; }

    [MsgPackKey]
    [Display(Name = "Device Name")]
    public string Name { get; set; } = string.Empty;

    [MsgPackKey]
    [StringLength(5000)]
    [Display(Name = "Notes")]
    public string Notes { get; set; } = string.Empty;

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
    [Display(Name = "Public IP")]
    public string PublicIP { get; set; } = string.Empty;

    [MsgPackKey]
    [StringLength(200)]
    [Display(Name = "Tags")]
    public string Tags { get; set; } = string.Empty;

    [MsgPackKey]
    [Display(Name = "Memory Total")]
    public double TotalMemory { get; set; }

    [MsgPackKey]
    [Display(Name = "Storage Total")]
    public double TotalStorage { get; set; }

    [MsgPackKey]
    [Display(Name = "Memory Used")]
    public double UsedMemory { get; set; }

    [IgnoreDataMember]
    [JsonIgnore]
    [Display(Name = "Memory Used %")]
    public double UsedMemoryPercent => UsedMemory / TotalMemory;

    [MsgPackKey]
    [Display(Name = "Storage Used")]
    public double UsedStorage { get; set; }

    [IgnoreDataMember]
    [JsonIgnore]
    [Display(Name = "Storage Used %")]
    public double UsedStoragePercent => UsedStorage / TotalStorage;
}