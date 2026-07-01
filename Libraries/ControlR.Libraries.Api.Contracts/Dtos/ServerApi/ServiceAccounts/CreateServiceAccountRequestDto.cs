using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.ServiceAccounts;

public record CreateServiceAccountRequestDto(
  [Required]
  [StringLength(100, MinimumLength = 1)]
  string Name,
  [StringLength(500)]
  string? Description);
