using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record UserStorageRequestDto(
  [property: RegularExpression("^[a-zA-Z0-9-]+$", ErrorMessage = "Storage key can only contain letters, numbers, and hyphens.")]
  [property: StringLength(256)]
  string Key,

  [property: StringLength(2048, ErrorMessage = "Storage value exceeds maximum length of 2048 characters.")]
  string Value);
