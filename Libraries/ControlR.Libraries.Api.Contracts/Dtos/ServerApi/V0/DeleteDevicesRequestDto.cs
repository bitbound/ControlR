using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

[MessagePackObject(keyAsPropertyName: true)]
public record DeleteDevicesRequestDto(Guid[] DeviceIds)
{
  public const int MaxDeviceIds = 1000;

  [MaxLength(MaxDeviceIds)]
  public Guid[] DeviceIds { get; init; } = DeviceIds;
}
