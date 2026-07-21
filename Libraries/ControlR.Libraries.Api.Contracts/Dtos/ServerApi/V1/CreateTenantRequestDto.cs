using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1;

public record CreateTenantRequestDto(
  [property: Required]
  [property: StringLength(100)]
  string Name);