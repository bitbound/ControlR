using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject]
public class DeviceUpdateResponseDto : DeviceUpdateRequestDto, IHasPrimaryKey
{
  [MsgPackKey]
  public string Alias { get; set; } = string.Empty;

  [IgnoreDataMember]
  [JsonIgnore]
  public double UsedMemoryPercent => UsedMemory / TotalMemory;

  [IgnoreDataMember]
  [JsonIgnore]
  public double UsedStoragePercent => UsedStorage / TotalStorage;
}