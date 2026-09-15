using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.DeviceGroups;

public record CreateDeviceGroupRequestDto(
  [property: Required]
  [property: StringLength(100, MinimumLength = 1)]
  string Name,

  [property: StringLength(500)]
  string? Description);