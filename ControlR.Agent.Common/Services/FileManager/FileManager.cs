using System.IO.Compression;
using ControlR.Libraries.Shared.Dtos.ServerApi;

namespace ControlR.Agent.Common.Services.FileManager;

public interface IFileManager
{
  Task<FileReferenceResult> CreateDirectory(string parentPath, string directoryName);
  Task<FileReferenceResult> CreateDirectory(string directoryPath);
  Task<FileReferenceResult> DeleteFileSystemEntry(string targetPath);
  Task<DirectoryContentsResult> GetDirectoryContents(string directoryPath);
  Task<PathSegmentsResponseDto> GetPathSegments(string targetPath);
  Task<FileSystemEntryDto[]> GetRootDrives();
  Task<FileSystemEntryDto[]> GetSubdirectories(string directoryPath);
  Task<FileReferenceResult> ResolveTargetFilePath(string targetPath);
  Task<FileReferenceResult> SaveUploadedFile(string targetDirectoryPath, string fileName, Stream fileStream, bool overwrite = false);
  Task<ValidateFilePathResponseDto> ValidateFilePath(string directoryPath, string fileName);
}

internal class FileManager(
  IFileSystem fileSystem,
  ILogger<FileManager> logger) : IFileManager
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<FileManager> _logger = logger;
  
  public Task<FileReferenceResult> CreateDirectory(string parentPath, string directoryName)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(parentPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("Parent path cannot be empty"));
      }

      if (string.IsNullOrWhiteSpace(directoryName))
      {
        return Task.FromResult(FileReferenceResult.Fail("Directory name cannot be empty"));
      }

      // Validate parent directory exists
      if (!_fileSystem.DirectoryExists(parentPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("Parent directory does not exist"));
      }

      // Combine paths using the platform-appropriate path separator
      var directoryPath = Path.Combine(parentPath, directoryName);

      return CreateDirectory(directoryPath);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating directory: {DirectoryName} in {ParentPath}", directoryName, parentPath);
      return Task.FromResult(FileReferenceResult.Fail(ex.Message));
    }
  }

  public Task<FileReferenceResult> CreateDirectory(string directoryPath)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(directoryPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("Directory path cannot be empty"));
      }

      if (_fileSystem.DirectoryExists(directoryPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("Directory already exists"));
      }

      if (_fileSystem.FileExists(directoryPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("A file with the same name already exists"));
      }

      _fileSystem.CreateDirectory(directoryPath);
      _logger.LogInformation("Successfully created directory: {DirectoryPath}", directoryPath);
      return FileReferenceResult
        .Ok(fileSystemPath: directoryPath, displayName: Path.GetFileName(directoryPath))
        .AsTaskResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating directory: {DirectoryPath}", directoryPath);
      return Task.FromResult(FileReferenceResult.Fail(ex.Message));
    }
  }

  public Task<FileReferenceResult> DeleteFileSystemEntry(string targetPath)
  {
    try
    {
      if (_fileSystem.DirectoryExists(targetPath))
      {
        _fileSystem.DeleteDirectory(targetPath, recursive: true);
        _logger.LogInformation("Successfully deleted directory: {DirectoryPath}", targetPath);
      }
      else if (_fileSystem.FileExists(targetPath))
      {
        _fileSystem.DeleteFile(targetPath);
        _logger.LogInformation("Successfully deleted file: {FilePath}", targetPath);
      }
      else
      {
        return Task.FromResult(FileReferenceResult.Fail("Target path does not exist"));
      }

      return FileReferenceResult
        .Ok(targetPath, Path.GetFileName(targetPath))
        .AsTaskResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deleting file system entry: {FilePath}", targetPath);
      return Task.FromResult(FileReferenceResult.Fail(ex.Message));
    }
  }

  public Task<DirectoryContentsResult> GetDirectoryContents(string directoryPath)
  {
    try
    {
      if (!_fileSystem.DirectoryExists(directoryPath))
      {
        _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
        return Task.FromResult(new DirectoryContentsResult([], false));
      }

      var entries = new List<FileSystemEntryDto>();

      // Get directories
      var directories = _fileSystem.GetDirectories(directoryPath)
        .Select(dirPath =>
        {
          try
          {
            var dirInfo = _fileSystem.GetDirectoryInfo(dirPath);
            return new FileSystemEntryDto(
              Name: dirInfo.Name,
              FullPath: dirInfo.FullName,
              IsDirectory: true,
              Size: 0,
              LastModified: dirInfo.LastWriteTime,
              IsHidden: dirInfo.Attributes.HasFlag(FileAttributes.Hidden),
              CanRead: !dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly),
              CanWrite: !dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly),
              HasSubfolders: HasSubdirectories(dirInfo.FullName));
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Error getting directory info for {DirectoryPath}", dirPath);
            return null;
          }
        })
        .Where(entry => entry is not null)
        .Cast<FileSystemEntryDto>();

      entries.AddRange(directories);

      // Get files
      var files = _fileSystem.GetFiles(directoryPath)
        .Select(filePath =>
        {
          try
          {
            var fileInfo = _fileSystem.GetFileInfo(filePath);
            return new FileSystemEntryDto(
              Name: fileInfo.Name,
              FullPath: fileInfo.FullName,
              IsDirectory: false,
              Size: fileInfo.Length,
              LastModified: fileInfo.LastWriteTime,
              IsHidden: fileInfo.Attributes.HasFlag(FileAttributes.Hidden),
              CanRead: true,
              CanWrite: !fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly),
              HasSubfolders: false); // Files don't have subfolders
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Error getting file info for {FilePath}", filePath);
            return null;
          }
        })
        .Where(entry => entry is not null)
        .Cast<FileSystemEntryDto>();

      entries.AddRange(files);

      var sortedEntries = entries
        .OrderBy(entry => entry.IsDirectory)
        .ThenBy(entry => entry.Name)
        .ToArray();

      return Task.FromResult(new DirectoryContentsResult(sortedEntries, true));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting directory contents for {DirectoryPath}", directoryPath);
      return Task.FromResult(new DirectoryContentsResult(Array.Empty<FileSystemEntryDto>(), false));
    }
  }

  public Task<PathSegmentsResponseDto> GetPathSegments(string targetPath)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(targetPath))
      {
        return Task.FromResult(new PathSegmentsResponseDto
        {
          Success = false,
          PathExists = false,
          PathSegments = [],
          ErrorMessage = "Target path cannot be empty"
        });
      }

      // Check if the path exists
      var pathExists = _fileSystem.DirectoryExists(targetPath);

      // Split the path into segments
      var normalizedPath = Path.GetFullPath(targetPath);
      var pathSegments = new List<string>();

      // Get the root (drive or root directory)
      var root = Path.GetPathRoot(normalizedPath);
      if (!string.IsNullOrEmpty(root))
      {
        pathSegments.Add(root);
      }

      // Get the remaining path segments
      var relativePath = Path.GetRelativePath(root ?? string.Empty, normalizedPath);
      if (!string.IsNullOrEmpty(relativePath) && relativePath != ".")
      {
        var segments = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], 
          StringSplitOptions.RemoveEmptyEntries);
        pathSegments.AddRange(segments);
      }

      return Task.FromResult(new PathSegmentsResponseDto
      {
        Success = true,
        PathExists = pathExists,
        PathSegments = [.. pathSegments]
      });
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting path segments for: {TargetPath}", targetPath);
      return Task.FromResult(new PathSegmentsResponseDto
      {
        Success = false,
        PathExists = false,
        PathSegments = [],
        ErrorMessage = $"Error getting path segments: {ex.Message}"
      });
    }
  }

  public Task<FileSystemEntryDto[]> GetRootDrives()
  {
    try
    {
      var drives = _fileSystem.GetDrives()
        .Where(d => d.IsReady && d.DriveType == DriveType.Fixed && d.TotalSize > 0)
        .Where(d => d.DriveFormat is not "squashfs" and not "overlay")
        .Where(d => !FileSystemConstants.ExcludedDrivePrefixes.Any(prefix =>
          d.RootDirectory.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        .Select(drive => new FileSystemEntryDto(
          Name: drive.Name,
          FullPath: drive.RootDirectory.FullName,
          IsDirectory: true,
          Size: 0,
          LastModified: DateTimeOffset.Now,
          IsHidden: false,
          CanRead: true,
          CanWrite: !drive.DriveType.Equals(DriveType.CDRom),
          HasSubfolders: HasSubdirectories(drive.RootDirectory.FullName)))
        .ToArray();

      return Task.FromResult(drives);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting root drives");
      return Task.FromResult(Array.Empty<FileSystemEntryDto>());
    }
  }

  public Task<FileSystemEntryDto[]> GetSubdirectories(string directoryPath)
  {
    try
    {
      if (!_fileSystem.DirectoryExists(directoryPath))
      {
        _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
        return Task.FromResult(Array.Empty<FileSystemEntryDto>());
      }

      var directories = _fileSystem.GetDirectories(directoryPath)
        .Select(dirPath =>
        {
          try
          {
            var dirInfo = _fileSystem.GetDirectoryInfo(dirPath);
            return new FileSystemEntryDto(
              Name: dirInfo.Name,
              FullPath: dirInfo.FullName,
              IsDirectory: true,
              Size: 0,
              LastModified: dirInfo.LastWriteTime,
              IsHidden: dirInfo.Attributes.HasFlag(FileAttributes.Hidden),
              CanRead: !dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly),
              CanWrite: !dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly),
              HasSubfolders: HasSubdirectories(dirInfo.FullName));
          }
          catch (Exception ex)
          {
            _logger.LogWarning(ex, "Error getting directory info for {DirectoryPath}", dirPath);
            return null;
          }
        })
        .Where(entry => entry is not null)
        .Cast<FileSystemEntryDto>()
        .OrderBy(entry => entry.Name)
        .ToArray();

      return Task.FromResult(directories);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting subdirectories for {DirectoryPath}", directoryPath);
      return Task.FromResult(Array.Empty<FileSystemEntryDto>());
    }
  }

  public async Task<FileReferenceResult> ResolveTargetFilePath(string filePath)
  {
    try
    {
      if (_fileSystem.DirectoryExists(filePath))
      {
        // Create a temporary ZIP file for the directory
        var tempZipPath = Path.Combine(Path.GetTempPath(), $"controlr-upload-{Guid.NewGuid()}.zip");

        // Use System.IO.Compression to create the ZIP
        await using var zipStream = _fileSystem.CreateFile(tempZipPath);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        await AddDirectoryToZip(archive, filePath, string.Empty);

        _logger.LogInformation("Successfully created ZIP file for directory: {DirectoryPath} -> {ZipPath}", filePath, tempZipPath);
        var directoryName = Path.GetFileName(filePath);
        return FileReferenceResult.Ok(
          fileSystemPath: tempZipPath,
          displayName: $"{directoryName}.zip",
          onDispose: () => _fileSystem.DeleteFile(tempZipPath));
      }
      else if (_fileSystem.FileExists(filePath))
      {
        // If it's a file, just return the original path
        return FileReferenceResult.Ok(
          fileSystemPath: filePath, 
          displayName: Path.GetFileName(filePath));
      }
      else
      {
        return FileReferenceResult.Fail("Target path does not exist");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error resolving target file path: {FilePath}", filePath);
      return FileReferenceResult.Fail(ex.Message);
    }
  }

  public async Task<FileReferenceResult> SaveUploadedFile(string targetDirectoryPath, string fileName, Stream fileStream, bool overwrite = false)
  {
    try
    {
      if (!_fileSystem.DirectoryExists(targetDirectoryPath))
      {
        return FileReferenceResult.Fail("Target directory does not exist");
      }

      var targetFilePath = Path.Combine(targetDirectoryPath, fileName);

      // Check if file already exists
      if (_fileSystem.FileExists(targetFilePath) && !overwrite)
      {
        return FileReferenceResult.Fail("File already exists");
      }

      await using var targetStream = _fileSystem.CreateFile(targetFilePath);
      await fileStream.CopyToAsync(targetStream);

      _logger.LogInformation("Successfully saved uploaded file: {FilePath}", targetFilePath);
      return FileReferenceResult.Ok(targetFilePath, fileName);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error saving uploaded file: {FileName} to {Directory}", fileName, targetDirectoryPath);
      return FileReferenceResult.Fail(ex.Message);
    }
  }

  public Task<ValidateFilePathResponseDto> ValidateFilePath(string directoryPath, string fileName)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(directoryPath))
      {
        return Task.FromResult(new ValidateFilePathResponseDto(false, "Directory path cannot be empty"));
      }

      if (string.IsNullOrWhiteSpace(fileName))
      {
        return Task.FromResult(new ValidateFilePathResponseDto(false, "File name cannot be empty"));
      }

      // Check if directory exists
      if (!_fileSystem.DirectoryExists(directoryPath))
      {
        return Task.FromResult(new ValidateFilePathResponseDto(false, "Directory does not exist"));
      }

      // Check for invalid characters in file name
      var invalidChars = Path.GetInvalidFileNameChars();
      if (fileName.IndexOfAny(invalidChars) >= 0)
      {
        return Task.FromResult(new ValidateFilePathResponseDto(false, "File name contains invalid characters"));
      }

      // Combine paths to get full file path
      var fullPath = Path.Combine(directoryPath, fileName);

      // Check if file already exists
      if (_fileSystem.FileExists(fullPath))
      {
        return Task.FromResult(new ValidateFilePathResponseDto(false, "File already exists"));
      }

      // Check if a directory with the same name exists
      if (_fileSystem.DirectoryExists(fullPath))
      {
        return Task.FromResult(new ValidateFilePathResponseDto(false, "A directory with the same name already exists"));
      }

      return Task.FromResult(new ValidateFilePathResponseDto(true));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error validating file path: {FileName} in {DirectoryPath}", fileName, directoryPath);
      return Task.FromResult(new ValidateFilePathResponseDto(false, $"Error validating path: {ex.Message}"));
    }
  }

  private async Task AddDirectoryToZip(ZipArchive archive, string directoryPath, string entryPrefix)
  {
    try
    {
      // Add all files in the directory
      var files = _fileSystem.GetFiles(directoryPath);
      foreach (var filePath in files)
      {
        var fileInfo = _fileSystem.GetFileInfo(filePath);
        var entryName = string.IsNullOrEmpty(entryPrefix)
          ? fileInfo.Name
          : $"{entryPrefix}/{fileInfo.Name}";

        var entry = archive.CreateEntry(entryName);
        await using var entryStream = entry.Open();
        await using var fileStream = _fileSystem.OpenFileStream(filePath, FileMode.Open, FileAccess.Read);
        await fileStream.CopyToAsync(entryStream);
      }

      // Recursively add subdirectories
      var directories = _fileSystem.GetDirectories(directoryPath);
      foreach (var subDirectoryPath in directories)
      {
        var dirInfo = _fileSystem.GetDirectoryInfo(subDirectoryPath);
        var newEntryPrefix = string.IsNullOrEmpty(entryPrefix)
          ? dirInfo.Name
          : $"{entryPrefix}/{dirInfo.Name}";

        await AddDirectoryToZip(archive, subDirectoryPath, newEntryPrefix);
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Error adding directory to ZIP: {DirectoryPath}", directoryPath);
      // Continue with other files/directories even if one fails
    }
  }

  private bool HasSubdirectories(string directoryPath)
  {
    try
    {
      if (!_fileSystem.DirectoryExists(directoryPath))
      {
        return false;
      }

      // Try to get at least one subdirectory to check if any exist
      var directories = _fileSystem.GetDirectories(directoryPath);
      return directories.Length > 0;
    }
    catch (Exception ex)
    {
      // If we can't access the directory (permissions, etc.), assume no subdirectories
      _logger.LogDebug(ex, "Could not check subdirectories for {DirectoryPath}", directoryPath);
      return false;
    }
  }
}
