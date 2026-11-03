using ControlR.Libraries.Branding;
using ControlR.Libraries.Shared.Services.FileSystem;

namespace ControlR.Agent.Shared.Services.Base;

/// <summary>
/// The install that predates the publisher becoming a directory in its paths, and the settings directory
/// that belongs to it.
/// </summary>
/// <param name="InstallDirectory">Where that install's executable lives.</param>
/// <param name="SettingsDirectory">Where that install keeps appsettings.json, which holds its signing key.</param>
public sealed record UnixPathRelocation(string InstallDirectory, string SettingsDirectory);

/// <summary>
/// TODO: Temporary migration code for v0.31.x
/// </summary>
/// <remarks>
/// <para>
/// One-time migration code holding the path shape that agents built before the publisher became a
/// directory, which is deliberately nowhere else in this codebase. Delete this file, its registration,
/// and its call sites in <see cref="Linux.AgentInstallerLinux"/> and <see cref="Mac.AgentInstallerMac"/>,
/// once no device reports an agent version below
/// the first release carrying the publisher directory.
/// </para>
/// <para>
/// Unlike Windows, no service registration has to be repointed here. Both installers rewrite their unit
/// file or plist on every install, and those names are brand-derived rather than publisher-derived, so
/// installing at the new path moves the service by itself. What is left is the identity handoff and the
/// old tree.
/// </para>
/// </remarks>
public interface ILegacyPathMigrationUnix
{
  /// <summary>
  /// Resolves the install to move, when this brand still has one at the path it used before the
  /// publisher became a directory.
  /// </summary>
  UnixPathRelocation? Plan();

  /// <summary>
  /// Removes the relocated install's directories, once the relocated service is confirmed running.
  /// </summary>
  void RemoveRelocatedInstall(UnixPathRelocation relocation);

  /// <summary>
  /// Copies the settings that carry this device's identity into the directory the relocated install reads
  /// its own settings from. Nothing under the old directories is touched here, which is what leaves the
  /// old install bootable if the move fails after this point.
  /// </summary>
  Result TryStageSettings(UnixPathRelocation relocation);
}

internal sealed class LegacyPathMigrationUnix(
  IFileSystem fileSystem,
  IFileSystemPathProvider pathProvider,
  ILogger<LegacyPathMigrationUnix> logger) : ILegacyPathMigrationUnix
{
  private const string AppSettingsFileName = "appsettings.json";

  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<LegacyPathMigrationUnix> _logger = logger;
  private readonly IFileSystemPathProvider _pathProvider = pathProvider;

  public UnixPathRelocation? Plan()
  {
    using var _ = _logger.BeginMemberScope();

    var instanceId = _pathProvider.GetEffectiveInstanceId();
    var legacyInstallDirectory = _pathProvider.GetAgentInstallDirectoryFor(
      publisherName: null,
      brandName: BrandingConstants.BrandName,
      instanceId: instanceId);

    if (!_fileSystem.DirectoryExists(legacyInstallDirectory))
    {
      return null;
    }

    return new UnixPathRelocation(
      legacyInstallDirectory,
      _pathProvider.GetSettingsDirectoryFor(
        publisherName: null,
        brandName: BrandingConstants.BrandName,
        instanceId: instanceId));
  }

  public void RemoveRelocatedInstall(UnixPathRelocation relocation)
  {
    using var _ = _logger.BeginMemberScope();

    // Best effort. The relocated install is running, and a directory left behind is recoverable in a way
    // that reporting a successful move as a failure is not.
    TryDeleteDirectory(relocation.InstallDirectory);
    TryDeleteDirectory(relocation.SettingsDirectory);
  }

  public Result TryStageSettings(UnixPathRelocation relocation)
  {
    using var _ = _logger.BeginMemberScope();

    var sourceSettingsPath = GetFile(relocation.SettingsDirectory, AppSettingsFileName);
    if (!_fileSystem.FileExists(sourceSettingsPath))
    {
      _logger.LogError(
        "Cannot relocate: no settings file at {SourceSettingsPath}, so this device's signing key could not " +
        "carry over. Nothing under {InstallDirectory} was changed.",
        sourceSettingsPath,
        relocation.InstallDirectory);
      return Result.Fail("This install's settings file was not found at the path it used before the publisher directory.");
    }

    var destinationDirectory = _pathProvider.GetSettingsDirectoryFor(
      publisherName: BrandingConstants.Publisher,
      brandName: BrandingConstants.BrandName,
      instanceId: _pathProvider.GetEffectiveInstanceId());

    try
    {
      _fileSystem.CreateDirectory(destinationDirectory);
      _fileSystem.CopyFile(sourceSettingsPath, GetFile(destinationDirectory, AppSettingsFileName), overwrite: true);

      var sourceHashPath = GetFile(relocation.SettingsDirectory, BrandingConstants.BundleHashFileName);
      if (_fileSystem.FileExists(sourceHashPath))
      {
        _fileSystem.CopyFile(
          sourceHashPath,
          GetFile(destinationDirectory, BrandingConstants.BundleHashFileName),
          overwrite: true);
      }

      _logger.LogInformation(
        "Staged this install's settings from {SourceSettingsPath} into {DestinationDirectory} for the relocated install to read.",
        sourceSettingsPath,
        destinationDirectory);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex,
        "Cannot relocate: the settings file could not be written into {DestinationDirectory}, so the relocated " +
        "install would register a new device. Nothing under {InstallDirectory} was changed.",
        destinationDirectory,
        relocation.InstallDirectory);
      return Result.Fail("Could not write the settings file at the relocated path.");
    }
  }

  private static string GetFile(string directoryPath, string fileName)
  {
    return Path.Join(directoryPath, fileName);
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
