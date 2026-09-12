using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.InstallerKeys;

/// <summary>
/// Body for <c>PUT /api/v1/installer-keys/{keyId}</c>. The key id travels in the route.
/// </summary>
public record RenameInstallerKeyRequestDto(
  [property: Required]
  [property: StringLength(100, MinimumLength = 1)]
  string FriendlyName);
