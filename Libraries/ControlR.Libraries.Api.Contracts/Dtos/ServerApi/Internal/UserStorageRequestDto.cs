using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record UserStorageRequestDto(
  [RegularExpression("^[a-zA-Z0-9-]+$", ErrorMessage = "Storage key can only contain letters, numbers, and hyphens.")]
  [StringLength(256)]
  string Key,

  [StringLength(2048, ErrorMessage = "Storage value exceeds maximum length of 2048 characters.")]
  string Value);
