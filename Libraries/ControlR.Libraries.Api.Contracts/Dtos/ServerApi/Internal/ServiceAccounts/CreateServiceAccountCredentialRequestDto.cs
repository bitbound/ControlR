using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal.ServiceAccounts;

public record CreateServiceAccountCredentialRequestDto(
  [Required]
  [StringLength(100, MinimumLength = 1)]
  string Name);
