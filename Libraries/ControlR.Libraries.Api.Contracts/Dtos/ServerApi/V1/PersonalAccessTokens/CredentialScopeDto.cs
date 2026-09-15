using System.ComponentModel.DataAnnotations;

namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;

/// <summary>
/// A single permission scope granted to a credential (PAT or logon token).
/// </summary>
public record CredentialScopeDto(
  [property: Required]
  [property: StringLength(150, MinimumLength = 1)]
  string PermissionName,

  PermissionScopeKind ScopeKind,

  Guid? ScopeId);
