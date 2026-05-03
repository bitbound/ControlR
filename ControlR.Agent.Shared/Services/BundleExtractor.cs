using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging;

namespace ControlR.Agent.Shared.Services;

public interface IBundleExtractor
{
  Task ExtractBundle(
    string bundlePath,
    string installDirectory,
    CancellationToken cancellationToken = default);
}

public class BundleExtractor(
  IFileSystem fileSystem,
  ILogger<BundleExtractor> logger)
  : IBundleExtractor
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<BundleExtractor> _logger = logger;

  public async Task ExtractBundle(
    string bundlePath,
    string installDirectory,
    CancellationToken cancellationToken = default)
  {
    _logger.LogInformation("Extracting bundle {BundlePath} to {InstallDir}", bundlePath, installDirectory);

    try
    {
      var normalizedInstallDirectory = Path.GetFullPath(installDirectory);
      _fileSystem.CreateDirectory(normalizedInstallDirectory);

      await _fileSystem.ExtractZipArchiveAsync(bundlePath, normalizedInstallDirectory, overwriteFiles: true, cancellationToken);
      SetKnownExecutablePermissions(normalizedInstallDirectory);
      await CleanupMacosxMetadata(normalizedInstallDirectory, cancellationToken);

      _logger.LogInformation("Bundle extracted successfully");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to extract bundle");
      throw;
    }
  }

  private Task CleanupMacosxMetadata(string installDirectory, CancellationToken cancellationToken)
  {
    var macosxPath = Path.Combine(installDirectory, "__MACOSX");
    if (_fileSystem.DirectoryExists(macosxPath))
    {
      _logger.LogInformation("Removing __MACOSX metadata directory.");
      _fileSystem.DeleteDirectory(macosxPath, true);
    }

    return Task.CompletedTask;
  }

  private void SetKnownExecutablePermissions(string installDirectory)
  {
    if (OperatingSystem.IsMacOS())
    {
      SetMacExecutablePermissions(installDirectory);
      return;
    }

    if (OperatingSystem.IsLinux())
    {
      SetLinuxExecutablePermissions(installDirectory);
    }
  }

  [System.Runtime.Versioning.SupportedOSPlatform("linux")]
  private void SetLinuxExecutablePermissions(string installDirectory)
  {
    var executableFileMode =
      UnixFileMode.UserRead |
      UnixFileMode.UserWrite |
      UnixFileMode.UserExecute |
      UnixFileMode.GroupRead |
      UnixFileMode.GroupExecute |
      UnixFileMode.OtherRead |
      UnixFileMode.OtherExecute;

    foreach (var executablePath in new[]
    {
      Path.Combine(installDirectory, "ControlR.Agent"),
      Path.Combine(installDirectory, "DesktopClient", "ControlR.DesktopClient")
    })
    {
      if (!_fileSystem.FileExists(executablePath))
      {
        continue;
      }

      _fileSystem.SetUnixFileMode(executablePath, executableFileMode);
      _logger.LogDebug("Set executable permissions on {FilePath}", executablePath);
    }
  }

  [System.Runtime.Versioning.SupportedOSPlatform("macos")]
  private void SetMacExecutablePermissions(string installDirectory)
  {
    var executableFileMode =
      UnixFileMode.UserRead |
      UnixFileMode.UserWrite |
      UnixFileMode.UserExecute |
      UnixFileMode.GroupRead |
      UnixFileMode.GroupExecute |
      UnixFileMode.OtherRead |
      UnixFileMode.OtherExecute;

    foreach (var executablePath in new[]
    {
      Path.Combine(installDirectory, "ControlR.app", "Contents", "MacOS", "ControlR.DesktopClient"),
      Path.Combine(installDirectory, "ControlR.app", "Contents", "Library", "LaunchServices", "ControlR.Agent")
    })
    {
      if (!_fileSystem.FileExists(executablePath))
      {
        continue;
      }

      _fileSystem.SetUnixFileMode(executablePath, executableFileMode);
      _logger.LogDebug("Set executable permissions on {FilePath}", executablePath);
    }
  }
}