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
/// installing at the new path moves the service by itself. What is left is the identity handoff, the old
/// tree, and putting those files back when a move fails after they were replaced. Only the previous
/// contents can do that, and they are gone once written over, so they are read here rather than recomputed.
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
  /// Reads the service definition files that this install is about to replace, so a move that fails after
  /// they are written can put the previous ones back. Both installers rewrite their unit file or plist on
  /// every install, so for a relocated install those files are what names the new path, and a failure
  /// later in the install would otherwise leave the old binary on disk with nothing able to start it.
  /// Files that do not exist yet are left out, since putting them back means leaving them absent.
  /// </summary>
  IReadOnlyDictionary<string, string> ReadServiceDefinitionFiles(params string[] filePaths);

  /// <summary>
  /// Removes the relocated install's directory and the two settings files this move carried over, once the
  /// relocated service is confirmed running. The rest of the old settings directory stays.
  /// </summary>
  void RemoveRelocatedInstall(UnixPathRelocation relocation);

  /// <summary>
  /// Writes back the service definition files read before this install replaced them. Best effort per
  /// file; the caller has already failed the install.
  /// </summary>
  Task RestoreServiceDefinitionFiles(IReadOnlyDictionary<string, string> previousContents);

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
  IOptionsAccessor optionsAccessor,
  ILogger<LegacyPathMigrationUnix> logger) : ILegacyPathMigrationUnix
{
  private const string AppSettingsFileName = "appsettings.json";

  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<LegacyPathMigrationUnix> _logger = logger;
  private readonly IOptionsAccessor _optionsAccessor = optionsAccessor;
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

  public IReadOnlyDictionary<string, string> ReadServiceDefinitionFiles(params string[] filePaths)
  {
    using var _ = _logger.BeginMemberScope();

    var contents = new Dictionary<string, string>();

    foreach (var filePath in filePaths)
    {
      if (_fileSystem.FileExists(filePath))
      {
        contents[filePath] = _fileSystem.ReadAllText(filePath);
      }
    }

    return contents;
  }

  public void RemoveRelocatedInstall(UnixPathRelocation relocation)
  {
    using var _ = _logger.BeginMemberScope();

    // Best effort. The relocated install is running, and a directory left behind is recoverable in a way
    // that reporting a successful move as a failure is not.
    TryDeleteDirectory(relocation.InstallDirectory);

    // Only the files this move carried over go. The desktop client keeps its own settings file and its
    // screen-sharing restore token in this same directory, so the tree is not this move's to discard.
    TryDeleteFile(GetFile(relocation.SettingsDirectory, AppSettingsFileName));
    TryDeleteFile(GetFile(relocation.SettingsDirectory, BrandingConstants.BundleHashFileName));
  }

  public async Task RestoreServiceDefinitionFiles(IReadOnlyDictionary<string, string> previousContents)
  {
    using var _ = _logger.BeginMemberScope();

    foreach (var (filePath, content) in previousContents)
    {
      try
      {
        await _fileSystem.WriteAllTextAsync(filePath, content);
        _logger.LogInformation("Restored {FilePath} to the service definition this install replaced.", filePath);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex,
          "Failed to restore {FilePath}. The install being replaced is still complete on disk, but nothing names " +
          "the path it runs from, so it cannot be started until the file is put back by hand.",
          filePath);
      }
    }
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
      var destinationSettingsPath = GetFile(destinationDirectory, AppSettingsFileName);
      _fileSystem.CopyFile(sourceSettingsPath, destinationSettingsPath, overwrite: true);

      // The file holds the device signing key in plaintext, so it gets the same restrictions the running
      // install puts on its own copy rather than whatever the copy inherited from the umask.
      _optionsAccessor.RestrictAccess(destinationSettingsPath);

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

  private void TryDeleteFile(string filePath)
  {
    try
    {
      if (_fileSystem.FileExists(filePath))
      {
        _fileSystem.DeleteFile(filePath);
        _logger.LogInformation("Removed {FilePath}, which the relocated install now has its own copy of.", filePath);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to remove {FilePath}. It can be deleted by hand.", filePath);
    }
  }
}
