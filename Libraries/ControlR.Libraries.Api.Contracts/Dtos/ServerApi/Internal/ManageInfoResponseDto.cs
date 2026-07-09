using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record ManageInfoResponseDto(
  [Required]
  string Email,
  bool IsEmailConfirmed);