using System.IO.Compression;
using ControlR.Agent.Shared.Constants;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Services.FileSystem;

namespace ControlR.Agent.Common.Services.FileManager;

/// <summary>
/// A service specifically intended to support the remote file system feature.
/// </summary>
public interface IFileManager
{
  Task<FileReferenceResult> CreateDirectory(string parentPath, string directoryName);
  Task<FileReferenceResult> CreateDirectory(string directoryPath);
  Task<FileReferenceResult> CreateDownloadArchive(string[] targetPaths, string archiveFileName);
  Task<FileReferenceResult> DeleteFileSystemEntry(string targetPath);
  Task<DirectoryContentsResult> GetDirectoryContents(string directoryPath);
  Task<List<LogFileGroupDto>> GetLogFiles();
  Task<PathSegmentsResponseDto> GetPathSegments(string targetPath);
  Task<FileSystemEntryDto[]> GetRootDrives();
  Task<FileSystemEntryDto[]> GetSubdirectories(string directoryPath);
  FileReferenceResult ResolveLogFile(LogKind kind, string fileName, string? username);
  Task<FileReferenceResult> ResolveTargetFilePath(string targetPath);
  Task<FileReferenceResult> SaveUploadedFile(string targetDirectoryPath, string fileName, Stream fileStream, bool overwrite = false);
  Task<ValidateFilePathResponseDto> ValidateFilePath(string directoryPath, string fileName);
}

internal class FileManager(
  IFileSystem fileSystem,
  IFileSystemPathProvider fileSystemPathProvider,
  ISystemEnvironment systemEnvironment,
  ILogger<FileManager> logger) : IFileManager
{
  private const string LogFilesSearchPattern = "LogFile*.log";

  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly IFileSystemPathProvider _fileSystemPathProvider = fileSystemPathProvider;
  private readonly ILogger<FileManager> _logger = logger;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  public Task<FileReferenceResult> CreateDirectory(string parentPath, string directoryName)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(parentPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("Parent path cannot be empty", OperationFailureCode.InvalidInput));
      }

      if (string.IsNullOrWhiteSpace(directoryName))
      {
        return Task.FromResult(FileReferenceResult.Fail("Directory name cannot be empty", OperationFailureCode.InvalidInput));
      }

      // Validate parent directory exists
      if (!_fileSystem.DirectoryExists(parentPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("Parent directory does not exist", OperationFailureCode.NotFound));
      }

      // Combine paths using the platform-appropriate path separator
      var directoryPath = Path.Combine(parentPath, directoryName);

      return CreateDirectory(directoryPath);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating directory: {DirectoryName} in {ParentPath}", directoryName, parentPath);
      return Task.FromResult(FileReferenceResult.Fail(ex.Message, OperationFailureCode.DeviceFailure));
    }
  }

  public Task<FileReferenceResult> CreateDirectory(string directoryPath)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(directoryPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("Directory path cannot be empty", OperationFailureCode.InvalidInput));
      }

      if (_fileSystem.DirectoryExists(directoryPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("Directory already exists", OperationFailureCode.AlreadyExists));
      }

      if (_fileSystem.FileExists(directoryPath))
      {
        return Task.FromResult(FileReferenceResult.Fail("A file with the same name already exists", OperationFailureCode.AlreadyExists));
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
      return Task.FromResult(FileReferenceResult.Fail(ex.Message, OperationFailureCode.DeviceFailure));
    }
  }

  public async Task<FileReferenceResult> CreateDownloadArchive(string[] targetPaths, string archiveFileName)
  {
    try
    {
      var normalizedPaths = targetPaths
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

      if (normalizedPaths.Length == 0)
      {
        return FileReferenceResult.Fail("At least one target path is required", OperationFailureCode.InvalidInput);
      }

      var normalizedArchiveFileName = ArchiveFileNameHelper.NormalizeArchiveFileName(archiveFileName);
      if (string.IsNullOrWhiteSpace(normalizedArchiveFileName))
      {
        return FileReferenceResult.Fail("Archive file name is invalid", OperationFailureCode.InvalidInput);
      }

      var tempZipPath = Path.Combine(Path.GetTempPath(), $"controlr-download-{Guid.NewGuid()}.zip");
      await using var zipStream = _fileSystem.CreateFile(tempZipPath);
      using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

      var rootEntryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      foreach (var targetPath in normalizedPaths)
      {
        if (_fileSystem.DirectoryExists(targetPath))
        {
          var directoryInfo = _fileSystem.GetDirectoryInfo(targetPath);
          var directoryEntryName = GetUniqueArchiveEntryName(rootEntryNames, directoryInfo.Name, isDirectory: true);
          await AddDirectoryToZip(archive, targetPath, directoryEntryName);
          continue;
        }

        if (_fileSystem.FileExists(targetPath))
        {
          var fileInfo = _fileSystem.GetFileInfo(targetPath);
          var fileEntryName = GetUniqueArchiveEntryName(rootEntryNames, fileInfo.Name, isDirectory: false);
          await AddFileToZip(archive, targetPath, fileEntryName);
          continue;
        }

        archive.Dispose();
        await zipStream.DisposeAsync();

        try
        {
          _fileSystem.DeleteFile(tempZipPath);
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Error cleaning up temporary ZIP file after failure: {ZipPath}", tempZipPath);
        }
        
        _logger.LogWarning("Target path does not exist: {TargetPath}", targetPath);
        return FileReferenceResult.Fail($"Target path does not exist: {targetPath}", OperationFailureCode.NotFound);
      }

      _logger.LogInformation(
        "Successfully created download archive {ArchiveFileName} with {ItemCount} item(s)",
        normalizedArchiveFileName,
        normalizedPaths.Length);

      return FileReferenceResult.Ok(tempZipPath, normalizedArchiveFileName, isTempFile: true);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error creating download archive {ArchiveFileName}", archiveFileName);
      return FileReferenceResult.Fail(ex.Message, OperationFailureCode.DeviceFailure);
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
        return Task.FromResult(FileReferenceResult.Fail("Target path does not exist", OperationFailureCode.NotFound));
      }

      return FileReferenceResult
        .Ok(targetPath, Path.GetFileName(targetPath))
        .AsTaskResult();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error deleting file system entry: {FilePath}", targetPath);
      return Task.FromResult(FileReferenceResult.Fail(ex.Message, OperationFailureCode.DeviceFailure));
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

  public Task<List<LogFileGroupDto>> GetLogFiles()
  {
    var logGroups = new List<LogFileGroupDto>();

    try
    {
      var agentLogs = GetAgentLogs();
      logGroups.Add(agentLogs);

      var installerLogs = GetInstallerLogs();
      logGroups.Add(installerLogs);

      if (_systemEnvironment.IsWindows())
      {
        var desktopLogs = GetWindowsDesktopClientLogs();
        logGroups.Add(desktopLogs);
      }
      else if (_systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS())
      {
        var desktopLogGroups = GetUnixDesktopClientLogs();
        logGroups.AddRange(desktopLogGroups);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error gathering log files");
    }

    return logGroups.AsTaskResult();
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

  public bool IsPathWithinLogRoots(string filePath)
  {
    if (string.IsNullOrWhiteSpace(filePath))
    {
      return false;
    }

    string fullPath;
    try
    {
      fullPath = Path.GetFullPath(filePath);
    }
    catch
    {
      return false;
    }

    var roots = GetLogRootDirectories().ToList();
    if (!roots.Any(root => IsWithinDirectory(fullPath, root)))
    {
      return false;
    }

    // Compare in resolved space. The roots and the candidate can disagree above
    // the root when the OS ships a link there (macOS /var -> /private/var), and a
    // path can sit inside a root and still stream a file outside it when the local
    // user planted a symlink inside that root. Resolving both sides keeps the
    // platform link working while a link that leaves the roots still lands outside
    // them and is refused. The agent typically runs elevated on Linux and macOS,
    // so following such a link exposes files the user cannot read directly.
    var resolvedRoots = roots
      .Select(ResolveRealPath)
      .Where(root => root is not null)
      .Cast<string>()
      .ToList();

    var resolvedPath = ResolveRealPath(fullPath);
    if (resolvedPath is null)
    {
      return false;
    }

    return resolvedRoots.Any(root => IsWithinDirectory(resolvedPath, root));
  }

  public FileReferenceResult ResolveLogFile(LogKind kind, string fileName, string? username)
  {
    if (!IsSinglePathSegment(fileName))
    {
      return FileReferenceResult.Fail("Log file name must be a single path segment.", OperationFailureCode.InvalidInput);
    }

    if (username is not null && !IsSinglePathSegment(username))
    {
      return FileReferenceResult.Fail("Username must be a single path segment.", OperationFailureCode.InvalidInput);
    }

    string? directoryPath;
    try
    {
      directoryPath = kind switch
      {
        LogKind.Agent when username is null => _fileSystemPathProvider.GetAgentLogsDirectoryPath(),
        LogKind.Installer when username is null => _fileSystemPathProvider.GetInstallerLogsDirectoryPath(),
        LogKind.DesktopClient when _systemEnvironment.IsWindows() && username is null =>
          _fileSystemPathProvider.GetWindowsDesktopClientLogsDirectory(),
        LogKind.DesktopClient when _systemEnvironment.IsLinux() || _systemEnvironment.IsMacOS() =>
          username is null
            ? _fileSystemPathProvider.GetUnixDesktopClientLogsDirectoryForRoot()
            : _fileSystemPathProvider.GetUnixDesktopClientLogsDirectory(username),
        _ => null
      };
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Could not resolve log directory for {LogKind}", kind);
      return FileReferenceResult.Fail("Log file selector is not valid for this device.", OperationFailureCode.InvalidInput);
    }

    if (directoryPath is null)
    {
      return FileReferenceResult.Fail("Log file selector is not valid for this device.", OperationFailureCode.InvalidInput);
    }

    try
    {
      var filePath = _fileSystem.GetFiles(directoryPath, LogFilesSearchPattern)
        .FirstOrDefault(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.Ordinal));

      if (filePath is null || !_fileSystem.FileExists(filePath))
      {
        return FileReferenceResult.Fail("Log file was not found.", OperationFailureCode.NotFound);
      }

      if (!IsPathWithinLogRoots(filePath))
      {
        return FileReferenceResult.Fail("Log file resolves outside an allowed log directory.", OperationFailureCode.PermissionDenied);
      }

      return FileReferenceResult.Ok(filePath, fileName);
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Could not enumerate log file {FileName} in {DirectoryPath}", fileName, directoryPath);
      return FileReferenceResult.Fail("Could not read log file.", OperationFailureCode.DeviceFailure);
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
          isTempFile: true);
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
        return FileReferenceResult.Fail("Target path does not exist", OperationFailureCode.NotFound);
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error resolving target file path: {FilePath}", filePath);
      return FileReferenceResult.Fail(ex.Message, OperationFailureCode.DeviceFailure);
    }
  }

  public async Task<FileReferenceResult> SaveUploadedFile(string targetDirectoryPath, string fileName, Stream fileStream, bool overwrite = false)
  {
    try
    {
      if (!_fileSystem.DirectoryExists(targetDirectoryPath))
      {
        return FileReferenceResult.Fail("Target directory does not exist", OperationFailureCode.NotFound);
      }

      var targetFilePath = Path.Combine(targetDirectoryPath, fileName);

      // Check if file already exists
      if (_fileSystem.FileExists(targetFilePath) && !overwrite)
      {
        return FileReferenceResult.Fail("File already exists", OperationFailureCode.AlreadyExists);
      }

      await using var targetStream = _fileSystem.CreateFile(targetFilePath);
      await fileStream.CopyToAsync(targetStream);

      _logger.LogInformation("Successfully saved uploaded file: {FilePath}", targetFilePath);
      return FileReferenceResult.Ok(targetFilePath, fileName);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error saving uploaded file: {FileName} to {Directory}", fileName, targetDirectoryPath);
      return FileReferenceResult.Fail(ex.Message, OperationFailureCode.DeviceFailure);
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

  private static string GetUniqueArchiveEntryName(HashSet<string> existingNames, string originalName, bool isDirectory)
  {
    var baseName = isDirectory
      ? originalName
      : Path.GetFileNameWithoutExtension(originalName);
    var extension = isDirectory ? string.Empty : Path.GetExtension(originalName);
    var candidate = originalName;
    var suffix = 2;

    while (!existingNames.Add(candidate))
    {
      candidate = $"{baseName} ({suffix}){extension}";
      suffix++;
    }

    return candidate;
  }

  private static bool IsSinglePathSegment(string value)
  {
    return !string.IsNullOrWhiteSpace(value)
      && value is not "." and not ".."
      && !value.Contains('\0')
      && !value.Contains('/')
      && !value.Contains('\\');
  }

  private static void TryAddLogRoot(ICollection<string> roots, Func<string> getRoot)
  {
    try
    {
      var value = getRoot();
      if (!string.IsNullOrWhiteSpace(value))
      {
        roots.Add(Path.GetFullPath(value));
      }
    }
    catch
    {
      // Provider log getters are platform-guarded and throw off-platform; that root simply does not apply.
    }
  }

  private async Task AddDirectoryToZip(ZipArchive archive, string directoryPath, string entryPrefix)
  {
    try
    {
      // Add all files in the directory
      var files = _fileSystem.GetFiles(directoryPath);
      var directories = _fileSystem.GetDirectories(directoryPath);

      if (files.Length == 0 && directories.Length == 0 && !string.IsNullOrWhiteSpace(entryPrefix))
      {
        archive.CreateEntry($"{entryPrefix}/");
        return;
      }

      foreach (var filePath in files)
      {
        var fileInfo = _fileSystem.GetFileInfo(filePath);
        var entryName = string.IsNullOrEmpty(entryPrefix)
          ? fileInfo.Name
          : $"{entryPrefix}/{fileInfo.Name}";

        await AddFileToZip(archive, filePath, entryName);
      }

      // Recursively add subdirectories
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

  private async Task AddFileToZip(ZipArchive archive, string filePath, string entryName)
  {
    var entry = archive.CreateEntry(entryName);
    await using var entryStream = entry.Open();
    await using var fileStream = _fileSystem.OpenFileStream(filePath, FileMode.Open, FileAccess.Read);
    await fileStream.CopyToAsync(entryStream);
  }

  private LogFileGroupDto GetAgentLogs()
  {
    var logFiles = new List<LogFileEntryDto>();

    try
    {
      var logsDir = _fileSystemPathProvider.GetAgentLogsDirectoryPath();
      if (_fileSystem.DirectoryExists(logsDir))
      {
        logFiles.AddRange(GetLogFilesFromDirectory(logsDir));
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting Windows agent logs");
    }

    return new LogFileGroupDto("Agent Logs", LogKind.Agent, null, logFiles);
  }

  private LogFileGroupDto GetInstallerLogs()
  {
    var logFiles = new List<LogFileEntryDto>();

    try
    {
      var logsDir = _fileSystemPathProvider.GetInstallerLogsDirectoryPath();
      if (_fileSystem.DirectoryExists(logsDir))
      {
        logFiles.AddRange(GetLogFilesFromDirectory(logsDir));
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting installer logs");
    }

    return new LogFileGroupDto("Installer Logs", LogKind.Installer, null, logFiles);
  }

  private List<LogFileEntryDto> GetLogFilesFromDirectory(string directoryPath)
  {
    var logFiles = new List<LogFileEntryDto>();

    try
    {
      var files = _fileSystem.GetFiles(directoryPath, LogFilesSearchPattern);

      foreach (var filePath in files)
      {
        try
        {
          var fileInfo = _fileSystem.GetFileInfo(filePath);
          logFiles.Add(new LogFileEntryDto(
            Path.GetFileName(filePath),
            filePath,
            fileInfo.Length,
            fileInfo.LastWriteTime));
        }
        catch (Exception ex)
        {
          _logger.LogDebug(ex, "Error getting info for log file: {FilePath}", filePath);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogDebug(ex, "Error reading log files from directory: {DirectoryPath}", directoryPath);
    }

    return logFiles.OrderByDescending(x => x.LastModified).ToList();
  }

  // The same roots GetLogFiles enumerates. The log-contents stream is constrained to these so a
  // principal with log-read on one device cannot point the agent at an arbitrary file it can open.
  private IEnumerable<string> GetLogRootDirectories()
  {
    var roots = new List<string>();

    TryAddLogRoot(roots, () => _fileSystemPathProvider.GetAgentLogsDirectoryPath());
    TryAddLogRoot(roots, () => _fileSystemPathProvider.GetInstallerLogsDirectoryPath());

    if (_systemEnvironment.IsWindows())
    {
      TryAddLogRoot(roots, () => _fileSystemPathProvider.GetWindowsDesktopClientLogsDirectory());
      return roots;
    }

    TryAddLogRoot(roots, () => _fileSystemPathProvider.GetUnixDesktopClientLogsDirectoryForRoot());
    var homeRoot = _systemEnvironment.IsMacOS() ? "/Users" : "/home";
    string[] homeDirectories;
    try
    {
      homeDirectories = _fileSystem.GetDirectories(homeRoot);
    }
    catch (Exception ex)
    {
      // /home can be missing or unreadable on minimal hosts. That only removes
      // the per-user roots; the roots gathered so far must still be honored.
      _logger.LogDebug(ex, "Could not enumerate per-user log roots under {HomeRoot}", homeRoot);
      return roots;
    }

    foreach (var homeDir in homeDirectories)
    {
      try
      {
        var username = _fileSystem.GetDirectoryInfo(homeDir).Name;
        TryAddLogRoot(roots, () => _fileSystemPathProvider.GetUnixDesktopClientLogsDirectory(username));
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "Skipping per-user log root for {HomeDir}", homeDir);
      }
    }

    return roots;
  }

  private List<LogFileGroupDto> GetUnixDesktopClientLogs()
  {
    var logGroups = new List<LogFileGroupDto>();

    try
    {
      var rootLogsDir = _fileSystemPathProvider.GetUnixDesktopClientLogsDirectoryForRoot();
      if (_fileSystem.DirectoryExists(rootLogsDir))
      {
        var rootLogs = GetLogFilesFromDirectory(rootLogsDir);
        logGroups.Add(new LogFileGroupDto("DesktopClient Logs (root)", LogKind.DesktopClient, null, rootLogs));
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting Unix desktop client logs for root user.");
    }

    try
    {
      var homeRoot = _systemEnvironment.IsMacOS() ? "/Users" : "/home";
      var homeDirectories = _fileSystem.GetDirectories(homeRoot);
      foreach (var homeDir in homeDirectories)
      {
        try
        {
          var dirInfo = _fileSystem.GetDirectoryInfo(homeDir);
          var username = dirInfo.Name;
          var userLogsPath = _fileSystemPathProvider.GetUnixDesktopClientLogsDirectory(username);

          if (_fileSystem.DirectoryExists(userLogsPath))
          {
            var userLogs = GetLogFilesFromDirectory(userLogsPath);
            logGroups.Add(new LogFileGroupDto($"DesktopClient Logs ({username})", LogKind.DesktopClient, username, userLogs));
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error getting desktop logs for user home directory: {HomeDir}", homeDir);
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting Unix desktop client logs");
    }

    return logGroups;
  }

  private LogFileGroupDto GetWindowsDesktopClientLogs()
  {
    var logFiles = new List<LogFileEntryDto>();

    try
    {
      var logsDir = _fileSystemPathProvider.GetWindowsDesktopClientLogsDirectory();
      if (_fileSystem.DirectoryExists(logsDir))
      {
        logFiles.AddRange(GetLogFilesFromDirectory(logsDir));
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting Windows desktop client logs");
    }

    return new LogFileGroupDto("DesktopClient Logs", LogKind.DesktopClient, null, logFiles);
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

  private bool IsWithinDirectory(string fullPath, string root)
  {
    var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    if (string.IsNullOrEmpty(normalizedRoot))
    {
      return false;
    }

    var comparison = _systemEnvironment.IsWindows()
      ? StringComparison.OrdinalIgnoreCase
      : StringComparison.Ordinal;

    if (string.Equals(fullPath, normalizedRoot, comparison))
    {
      return true;
    }

    return fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison);
  }

  // Resolves every link along a path the way the OS does on open, so two paths
  // that reach the same file compare equal. ResolveLinkTarget inspects only the
  // final component, so each segment is resolved in turn. Returns null when a
  // component cannot be inspected, which the caller treats as a refusal.
  private string? ResolveRealPath(string fullPath)
  {
    var pathRoot = Path.GetPathRoot(fullPath);
    if (string.IsNullOrEmpty(pathRoot))
    {
      return null;
    }

    var resolved = pathRoot;
    foreach (var segment in fullPath[pathRoot.Length..]
      .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
    {
      resolved = Path.Join(resolved, segment);

      IFileSystemItemInfo? target;
      try
      {
        target = _fileSystem.ResolveLinkTarget(resolved, returnFinalTarget: true);
      }
      catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
      {
        // A component that is not on disk is not a link. Existence is not this
        // method's concern.
        continue;
      }
      catch (Exception ex)
      {
        _logger.LogDebug(ex, "Could not resolve link target for {Path}", resolved);
        return null;
      }

      if (target is not null)
      {
        resolved = Path.GetFullPath(target.FullName);
      }
    }

    return resolved;
  }
}

