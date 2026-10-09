namespace ControlR.Agent.Shared.Models;

public sealed record AgentInstallRequest
{
  public string? BundleSha256 { get; init; }
  public required string BundleZipPath { get; init; }
  public Guid? CustomerId { get; init; }
  public Guid? DeviceId { get; init; }
  public Guid? InstallerKeyId { get; init; }
  public string? InstallerKeySecret { get; init; }

  /// <summary>
  /// Brand name of the install this one replaces, set only during a migration. That install's service
  /// is stopped before this one starts, and removed once this one is running.
  /// </summary>
  public string? PreviousBrandName { get; init; }

  /// <summary>
  /// Instance id of the install this one replaces, set only during a migration. Null means that
  /// install used the default instance id. Together with <see cref="PreviousBrandName"/> it names the
  /// install to stop and remove, which is not always the one this install is going to.
  /// </summary>
  public string? PreviousInstanceId { get; init; }

  public required Uri ServerUri { get; init; }
  public Guid[]? TagIds { get; init; }
  public required Guid TenantId { get; init; }
}