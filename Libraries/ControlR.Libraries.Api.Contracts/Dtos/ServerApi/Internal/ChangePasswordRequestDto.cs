using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record ChangePasswordRequestDto(
  [Required]
  string CurrentPassword,
  [Required]
  string NewPassword);