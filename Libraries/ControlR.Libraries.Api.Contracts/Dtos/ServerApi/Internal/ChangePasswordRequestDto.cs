using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record ChangePasswordRequestDto(
  [property: Required]
  string CurrentPassword,
  [property: Required]
  string NewPassword);