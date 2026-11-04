using System.Runtime.Versioning;
using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Services.FileSystem;

namespace ControlR.Agent.Shared.Services.Windows;

/// <summary>
/// The install that lives at the path this brand used before the directory it is being moved out of, and
/// the settings directory that belongs to it.
/// </summary>
/// <param name="InstallDirectory">Where that install's executable is registered to run from.</param>
/// <param name="SettingsDirectory">Where that install keeps appsettings.json, which holds its signing key.</param>
public sealed record WindowsPathRelocation(string InstallDirectory, string SettingsDirectory);

/// <summary>
/// TODO: Temporary migration code for v0.31.x
/// </summary>
/// <remarks>
/// <para>
/// One-time migration code. It names the directory an existing install was written into so that install can
/// be found, moved, and then removed, none of which the installer could do by formula alone: the install
/// already on the machine was written by a build whose compiled branding put it somewhere else. Once no
/// device reports an agent version below the first release carrying this, delete this file, its
/// registration, and its call sites in <see cref="AgentInstallerWindows"/>, and the permanent guard there
/// takes over every case.
/// </para>
/// <para>
/// It runs inside the installer rather than the agent because the agent that is installed is the one
/// with the old formula compiled in. It cannot be told about a path it has no name for, but it already
/// launches this installer on any bundle hash mismatch, which is the handover this relies on.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public interface ILegacyPathMigrationWindows
{
  /// <summary>
  /// Resolves the relocation for a service that is registered to run outside the directory this build
  /// computes for itself.
  /// </summary>
  /// <param name="previousPublisher">
  /// The vendor the resident install reported when it started this migration, which is the directory its
  /// paths sit under. Null is the install from before the publisher became a directory at all.
  /// </param>
  /// <param name="registeredDirectory">
  /// The directory the service is registered to run from, which is the only machine state that records
  /// where the existing install's files are.
  /// </param>
  /// <returns>
  /// The install to move, or null when the registered directory is not a layout this class can name. A
  /// caller must not move a layout it cannot also find again, and must not remove a directory it did not
  /// write.
  /// </returns>
  WindowsPathRelocation? Plan(string? previousPublisher, string registeredDirectory);

  /// <summary>
  /// Removes the relocated install's directories. Call this only once the relocated service is running.
  /// </summary>
  void RemoveRelocatedInstall(WindowsPathRelocation relocation);

  /// <summary>
  /// Copies the settings that carry this device's identity into the directory the relocated install reads
  /// its own settings from. Nothing under the old directories is touched here, which is what leaves the
  /// old install bootable if the move fails after this point.
  /// </summary>
  bool TryStageSettings(WindowsPathRelocation relocation);
}

[SupportedOSPlatform("windows")]
internal sealed class LegacyPathMigrationWindows(
  IFileSystem fileSystem,
  IFileSystemPathProvider pathProvider,
  IOptionsAccessor optionsAccessor,
  ILogger<LegacyPathMigrationWindows> logger) : ILegacyPathMigrationWindows
{
  private const string AppSettingsFileName = "appsettings.json";

  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<LegacyPathMigrationWindows> _logger = logger;
  private readonly IOptionsAccessor _optionsAccessor = optionsAccessor;
  private readonly IFileSystemPathProvider _pathProvider = pathProvider;

  public WindowsPathRelocation? Plan(string? previousPublisher, string registeredDirectory)
  {
    using var _ = _logger.BeginMemberScope();

    var instanceSegment = GetLastSegment(registeredDirectory);
    var instanceId = string.Equals(instanceSegment, AppConstants.DefaultInstanceId, StringComparison.OrdinalIgnoreCase)
      ? null
      : instanceSegment;

    // The brand and instance come from the registration itself, and the vendor from what the resident
    // install reported, so this names the directory that install was written into by formula rather than
    // by guessing at whatever precedes the brand segment. A layout no identity produces is not moved.
    var previousDirectory = _pathProvider.GetAgentInstallDirectoryFor(
      publisherName: previousPublisher,
      brandName: BrandingConstants.BrandName,
      instanceId: instanceId);

    if (!SameDirectory(previousDirectory, registeredDirectory))
    {
      _logger.LogInformation(
        "The service is registered at {RegisteredDirectory}, which is not the path brand {BrandName} would " +
        "have been written at under publisher {PreviousPublisher}, so it is left where it is.",
        registeredDirectory,
        BrandingConstants.BrandName,
        previousPublisher ?? "the absence of one");
      return null;
    }

    return new WindowsPathRelocation(
      registeredDirectory,
      _pathProvider.GetSettingsDirectoryFor(
        publisherName: previousPublisher,
        brandName: BrandingConstants.BrandName,
        instanceId: instanceId));
  }

  public void RemoveRelocatedInstall(WindowsPathRelocation relocation)
  {
    using var _ = _logger.BeginMemberScope();

    // Best effort. The relocated install is running, and a directory left behind is recoverable in a way
    // that reporting a successful move as a failure is not.
    TryDeleteDirectory(relocation.InstallDirectory);
    TryDeleteDirectory(relocation.SettingsDirectory);
  }

  public bool TryStageSettings(WindowsPathRelocation relocation)
  {
    using var _ = _logger.BeginMemberScope();

    var sourceSettingsPath = GetFile(relocation.SettingsDirectory, AppSettingsFileName);
    if (!_fileSystem.FileExists(sourceSettingsPath))
    {
      _logger.LogError(
        "Cannot relocate: no settings file at {SourceSettingsPath}, so this device's signing key could not " +
        "carry over. Nothing on this machine was changed.",
        sourceSettingsPath);
      return false;
    }

    var destinationDirectory = _pathProvider.GetSettingsDirectoryFor(
      publisherName: BrandingConstants.Publisher,
      brandName: BrandingConstants.BrandName,
      instanceId: _pathProvider.GetEffectiveInstanceId());

    try
    {
      _fileSystem.CreateDirectory(destinationDirectory);
      var destinationSettingsPath = GetFile(destinationDirectory, AppSettingsFileName);
      _fileSystem.CopyFile(sourceSettingsPath, destinationSettingsPath, overwrite: true);

      // The file holds the device signing key in plaintext, so it gets the same restrictions the running
      // install puts on its own copy rather than whatever the copy inherited.
      _optionsAccessor.RestrictAccess(destinationSettingsPath);

      var hashFileName = BrandingConstants.BundleHashFileName;
      var sourceHashPath = GetFile(relocation.SettingsDirectory, hashFileName);
      if (_fileSystem.FileExists(sourceHashPath))
      {
        _fileSystem.CopyFile(sourceHashPath, GetFile(destinationDirectory, hashFileName), overwrite: true);
      }

      _logger.LogInformation(
        "Staged this install's settings from {SourceSettingsPath} into {DestinationDirectory} for the relocated install to read.",
        sourceSettingsPath,
        destinationDirectory);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex,
        "Cannot relocate: the settings file could not be written into {DestinationDirectory}, so the relocated " +
        "install would register a new device. Nothing under {InstallDirectory} was changed.",
        destinationDirectory,
        relocation.InstallDirectory);
      return false;
    }
  }

  private static string GetLastSegment(string directoryPath)
  {
    var trimmed = TrimEnd(directoryPath);
    var lastSeparator = trimmed.LastIndexOf('\\');
    return lastSeparator < 0 ? trimmed : trimmed[(lastSeparator + 1)..];
  }

  private static bool SameDirectory(string first, string second)
  {
    return string.Equals(TrimEnd(first), TrimEnd(second), StringComparison.OrdinalIgnoreCase);
  }

  private static string TrimEnd(string directoryPath)
  {
    return directoryPath.TrimEnd('\\');
  }

  private string GetFile(string directoryPath, string fileName)
  {
    return _fileSystem.JoinPaths('\\', directoryPath, fileName);
  }

  private void TryDeleteDirectory(string directoryPath)
  {
    try
    {
      if (_fileSystem.DirectoryExists(directoryPath))
      {
        _fileSystem.DeleteDirectory(directoryPath, true);
        _logger.LogInformation("Removed {DirectoryPath}, which the relocated install no longer uses.", directoryPath);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to remove {DirectoryPath}. It can be deleted by hand.", directoryPath);
    }
  }
}
