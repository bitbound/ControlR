using ControlR.Libraries.Shared.Dtos.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

[MessagePackObject]
public class DeviceResponseDto : DeviceRequestDto, IEntityBaseDto
{
    [MsgPackKey]
    public string Alias { get; set; } = string.Empty;

    [MsgPackKey]
    public Guid? DeviceGroupId { get; set; }

    [IgnoreDataMember]
    [JsonIgnore]
    public double UsedMemoryPercent => UsedMemory / TotalMemory;

    [IgnoreDataMember]
    [JsonIgnore]
    public double UsedStoragePercent => UsedStorage / TotalStorage;
}