using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos;

[MessagePackObject]
public class DeviceDto : DeviceFromAgentDto
{
  [MsgPackKey]
  [Display(Name = "Alias")]
  public string Alias { get; set; } = string.Empty;

  [MsgPackKey]
  public string ConnectionId { get; set; } = string.Empty;

  [MsgPackKey]
  public int? DeviceGroupId { get; set; }

  [MsgPackKey]
  public int TenantId { get; set; }

  [IgnoreDataMember]
  [JsonIgnore]
  [Display(Name = "Memory Used %")]
  public double UsedMemoryPercent => UsedMemory / TotalMemory;

  [IgnoreDataMember]
  [JsonIgnore]
  [Display(Name = "Storage Used %")]
  public double UsedStoragePercent => UsedStorage / TotalStorage;
}