using System.IO.Compression;
using ControlR.Agent.Common.Services.FileManager;
using ControlR.Libraries.Shared.Dtos.ServerApi;

namespace ControlR.Agent.Common.Services.FileManager;

public interface IFileManager
{
  Task<FileReferenceResult> CreateDirectory(string directoryPath);
  Task<FileReferenceResult> DeleteFileSystemEntry(string targetPath);
  Task<DirectoryContentsResult> GetDirectoryContents(string directoryPath);
  Task<FileSystemEntryDto[]> GetRootDrives();
  Task<FileSystemEntryDto[]> GetSubdirectories(string directoryPath);
  Task<FileReferenceResult> ResolveTargetFilePath(string targetPath);
  Task<FileReferenceResult> SaveUploadedFile(string targetDirectoryPath, string fileName, Stream fileStream, bool overwrite = false);
}

internal class FileManager(
  IFileSystem fileSystem,
  ILogger<FileManager> logger) : IFileManager
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<FileManager> _logger = logger;

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
      return Task.FromResult(FileReferenceResult.Ok(directoryPath));
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

      return Task.FromResult(FileReferenceResult.Ok(targetPath));
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
        return Task.FromResult(new DirectoryContentsResult(Array.Empty<FileSystemEntryDto>(), false));
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

      return Task.FromResult(new DirectoryContentsResult(entries.ToArray(), true));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting directory contents for {DirectoryPath}", directoryPath);
      return Task.FromResult(new DirectoryContentsResult(Array.Empty<FileSystemEntryDto>(), false));
    }
  }

  public Task<FileSystemEntryDto[]> GetRootDrives()
  {
    try
    {
      var drives = _fileSystem.GetDrives()
        .Where(d => d.IsReady)
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
        using var zipStream = _fileSystem.CreateFile(tempZipPath);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        await AddDirectoryToZip(archive, filePath, string.Empty);

        _logger.LogInformation("Successfully created ZIP file for directory: {DirectoryPath} -> {ZipPath}", filePath, tempZipPath);
        return FileReferenceResult.Ok(tempZipPath, onDispose: () => _fileSystem.DeleteFile(tempZipPath));
      }
      else if (_fileSystem.FileExists(filePath))
      {
        // If it's a file, just return the original path
        return FileReferenceResult.Ok(filePath);
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

      using var targetStream = _fileSystem.CreateFile(targetFilePath);
      await fileStream.CopyToAsync(targetStream);

      _logger.LogInformation("Successfully saved uploaded file: {FilePath}", targetFilePath);
      return FileReferenceResult.Ok(targetFilePath);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error saving uploaded file: {FileName} to {Directory}", fileName, targetDirectoryPath);
      return FileReferenceResult.Fail(ex.Message);
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
        using var entryStream = entry.Open();
        using var fileStream = _fileSystem.OpenFileStream(filePath, FileMode.Open, FileAccess.Read);
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
