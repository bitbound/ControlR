namespace ControlR.Web.Server.Services.Authorization;

public sealed record ServiceAccountSnapshot(
  string Name,
  ServiceAccountKind Kind,
  string? Description,
  bool IsEnabled);

public sealed record ServiceAccountCredentialSnapshot(
  string Name,
  Guid ServiceAccountId);

public sealed record CredentialScopeSnapshot(
  string PermissionName,
  PermissionScopeKind ScopeKind,
  Guid? ScopeId);

public sealed record CredentialScopeSetSummary(int ScopeCount);

/// <summary>
/// Written before orphan-token cleanup so a failed creation attempt leaves a trail in
/// <see cref="AuthorizationChangeLog"/>.
/// </summary>
public sealed record CredentialScopeSetFailureSummary(int ScopeCount, string Reason);

/// <summary>
/// One entry is written per seed call rather than per seeded row, so bootstrap grants are
/// auditable without per-row noise.
/// </summary>
public sealed record PermissionAssignmentSeedSummary(int Count, IReadOnlyList<string> Presets);

public sealed record CustomerSnapshot(string Name, string? Description, string? Notes);

public sealed record CustomerDeviceAssignmentChange(int Count);

public sealed record DeviceGroupSnapshot(string Name, string? Description);

public sealed record DeviceGroupMembershipChange(int Count);

public sealed record UserGroupSnapshot(string Name, string? Description);

public sealed record UserGroupMembershipChange(int Count);

public sealed record PermissionAssignmentSnapshot(
  string PermissionName,
  PermissionEffect Effect,
  PermissionScopeKind ScopeKind,
  Guid? ScopeId);
