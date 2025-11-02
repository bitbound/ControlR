using System.Reflection;
using System.Security.Cryptography;
using ControlR.Libraries.Shared.Constants;

namespace ControlR.Agent.Common.Services;

public interface IEmbeddedDesktopClientProvider
{
  Task<Result<string>> ExtractDesktopClient(string targetZipPath, CancellationToken cancellationToken);
  Task<Result<string>> GetEmbeddedResourceHash(CancellationToken cancellationToken);
}

internal class EmbeddedDesktopClientProvider(
  IFileSystem fileSystem,
  ILogger<EmbeddedDesktopClientProvider> logger) : IEmbeddedDesktopClientProvider
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<EmbeddedDesktopClientProvider> _logger = logger;

  public async Task<Result<string>> ExtractDesktopClient(string targetZipPath, CancellationToken cancellationToken)
  {
    try
    {
      var resourceName = GetEmbeddedResourceName();
      var assembly = Assembly.GetExecutingAssembly();

      _logger.LogInformation("Attempting to extract embedded desktop client resource: {ResourceName}", resourceName);

      using var resourceStream = assembly.GetManifestResourceStream(resourceName);
      if (resourceStream is null)
      {
        var availableResources = assembly.GetManifestResourceNames();
        _logger.LogWarning(
          "Embedded resource not found: {ResourceName}. Available resources: {AvailableResources}",
          resourceName,
          string.Join(", ", availableResources));
        return Result.Fail<string>("Embedded desktop client resource not found.");
      }

      // Ensure target directory exists
      var targetDir = Path.GetDirectoryName(targetZipPath);
      if (!string.IsNullOrEmpty(targetDir) && !_fileSystem.DirectoryExists(targetDir))
      {
        _fileSystem.CreateDirectory(targetDir);
      }

      // Extract the resource to the target path
      await using var fileStream = _fileSystem.OpenFileStream(
        targetZipPath,
        FileMode.Create,
        FileAccess.Write,
        FileShare.None);

      await resourceStream.CopyToAsync(fileStream, cancellationToken);
      await fileStream.FlushAsync(cancellationToken);

      _logger.LogInformation("Successfully extracted embedded desktop client to: {TargetPath}", targetZipPath);
      return Result.Ok(targetZipPath);
    }
    catch (Exception ex)
    {
      return Result.Fail<string>(ex, "Failed to extract embedded desktop client.");
    }
  }

  public async Task<Result<string>> GetEmbeddedResourceHash(CancellationToken cancellationToken)
  {
    try
    {
      var resourceName = GetEmbeddedResourceName();
      var assembly = Assembly.GetExecutingAssembly();

      await using var resourceStream = assembly.GetManifestResourceStream(resourceName);
      if (resourceStream is null)
      {
        return Result.Fail<string>("Embedded desktop client resource not found.");
      }

      var hashBytes = await SHA256.HashDataAsync(resourceStream, cancellationToken);
      var hexHash = Convert.ToHexString(hashBytes);
      
      return Result.Ok(hexHash);
    }
    catch (Exception ex)
    {
      return Result.Fail<string>(ex, "Failed to compute hash of embedded desktop client.");
    }
  }

  private static string GetEmbeddedResourceName()
  {
    var zipFileName = AppConstants.DesktopClientZipFileName;
    var assembly = typeof(EmbeddedDesktopClientProvider).Assembly;
    var rootNamespace = assembly.GetName().Name;
    return $"{rootNamespace}.Resources.{zipFileName}";
  }
}
