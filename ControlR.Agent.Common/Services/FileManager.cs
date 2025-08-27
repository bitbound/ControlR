using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Dtos.ServerApi;

namespace ControlR.Agent.Common.Services;

internal class FileManager(
  IFileSystem fileSystem,
  ILogger<FileManager> logger) : IFileManager
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly ILogger<FileManager> _logger = logger;

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
          CanWrite: !drive.DriveType.Equals(DriveType.CDRom)))
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
              CanWrite: !dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly));
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

  public Task<FileSystemEntryDto[]> GetDirectoryContents(string directoryPath)
  {
    try
    {
      if (!_fileSystem.DirectoryExists(directoryPath))
      {
        _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
        return Task.FromResult(Array.Empty<FileSystemEntryDto>());
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
              CanWrite: !dirInfo.Attributes.HasFlag(FileAttributes.ReadOnly));
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
              CanWrite: !fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly));
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

      return Task.FromResult(entries.ToArray());
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting directory contents for {DirectoryPath}", directoryPath);
      return Task.FromResult(Array.Empty<FileSystemEntryDto>());
    }
  }
}
