using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0.ServiceAccounts;

public record CreateServiceAccountCredentialRequestDto(
  [Required]
  [StringLength(100, MinimumLength = 1)]
  string Name);

