using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;

public record CreateServiceAccountRequestDto(
  [property: Required]
  [property: StringLength(100, MinimumLength = 1)]
  string Name,
  [property: StringLength(500)]
  string? Description);

