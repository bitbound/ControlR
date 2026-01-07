using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.DevicesCommon.Services;

public interface IFileSystem
{
  Task AppendAllLinesAsync(string path, IEnumerable<string> lines);

  void CopyFile(string sourceFile, string destinationFile, bool overwrite);

  DirectoryInfo CreateDirectory(string directoryPath);

  Stream CreateFile(string filePath);

  FileStream CreateFileStream(string filePath, FileMode mode);

  void DeleteDirectory(string directoryPath, bool recursive);

  void DeleteFile(string filePath);

  bool DirectoryExists(string directoryPath);

  void ExtractZipArchive(string sourceArchiveFileName, string destinationDirectoryName, bool overwriteFiles);

  bool FileExists(string path);

  string[] GetDirectories(string path);

  DirectoryInfo GetDirectoryInfo(string directoryPath);

  DriveInfo[] GetDrives();

  FileInfo GetFileInfo(string filePath);

  FileVersionInfo GetFileVersionInfo(string filePath);

  string[] GetFiles(string path);

  public string[] GetFiles(string path, string searchPattern);

  public string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

  public string[] GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions);

  string JoinPaths(char separator, params string[] paths);

  void MoveFile(string sourceFile, string destinationFile, bool overwrite);

  FileStream OpenFileStream(string path, FileMode mode, FileAccess access);

  FileStream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare fileShare);

  Task<byte[]> ReadAllBytesAsync(string path);

  Task<string[]> ReadAllLinesAsync(string path);

  string ReadAllText(string filePath);

  Task<string> ReadAllTextAsync(string path);

  Task ReplaceLineInFile(string filePath, string matchPattern, string replaceLineWith, int maxMatches = -1);

  /// <summary>
  /// Resolves the absolute file path for the specified file name using `which` on Unix-based systems or `where.exe` on Windows.
  /// </summary>
  /// <param name="fileName">The name of the file to resolve. Cannot be null or empty.</param>
  /// <returns>A task that represents the asynchronous operation. The result contains the absolute file path if the file is
  /// found; otherwise, an error result.</returns>
  Task<Result<string>> ResolveFilePath(string fileName);

  [SupportedOSPlatform("linux")]
  [SupportedOSPlatform("macos")]
  void SetUnixFileMode(string filePath, UnixFileMode fileMode);

  Task WriteAllBytesAsync(string path, byte[] buffer, CancellationToken cancellationToken = default);

  Task WriteAllLines(string path, List<string> lines);

  void WriteAllText(string filePath, string contents);

  Task WriteAllTextAsync(string path, string content);
}

public class FileSystem(ILogger<FileSystem> logger) : IFileSystem
{
  private readonly ILogger<FileSystem> _logger = logger;

  public Task AppendAllLinesAsync(string path, IEnumerable<string> lines)
  {
    return File.AppendAllLinesAsync(path, lines);
  }

  public void CopyFile(string sourceFile, string destinationFile, bool overwrite)
  {
    File.Copy(sourceFile, destinationFile, overwrite);
  }

  public DirectoryInfo CreateDirectory(string directoryPath)
  {
    return Directory.CreateDirectory(directoryPath);
  }

  public Stream CreateFile(string filePath)
  {
    return File.Create(filePath);
  }

  public FileStream CreateFileStream(string filePath, FileMode mode)
  {
    return new FileStream(filePath, mode);
  }

  public void DeleteDirectory(string directoryPath, bool recursive)
  {
    Directory.Delete(directoryPath, recursive);
  }

  public void DeleteFile(string filePath)
  {
    File.Delete(filePath);
  }

  public bool DirectoryExists(string directoryPath)
  {
    return Directory.Exists(directoryPath);
  }

  public void ExtractZipArchive(string sourceArchiveFileName, string destinationDirectoryName, bool overwriteFiles)
  {
    System.IO.Compression.ZipFile.ExtractToDirectory(sourceArchiveFileName, destinationDirectoryName, overwriteFiles);
  }

  public bool FileExists(string path)
  {
    return File.Exists(path);
  }

  public string[] GetDirectories(string path)
  {
    return Directory.GetDirectories(path);
  }

  public DirectoryInfo GetDirectoryInfo(string directoryPath)
  {
    return new DirectoryInfo(directoryPath);
  }

  public DriveInfo[] GetDrives()
  {
    return DriveInfo.GetDrives();
  }

  public FileInfo GetFileInfo(string filePath)
  {
    return new FileInfo(filePath);
  }

  public FileVersionInfo GetFileVersionInfo(string filePath)
  {
    return FileVersionInfo.GetVersionInfo(filePath);
  }

  public string[] GetFiles(string path)
  {
    return Directory.GetFiles(path);
  }

  public string[] GetFiles(string path, string searchPattern)
  {
    return Directory.GetFiles(path, searchPattern);
  }

  public string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
  {
    return Directory.GetFiles(path, searchPattern, searchOption);
  }

  public string[] GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions)
  {
    return Directory.GetFiles(path, searchPattern, enumerationOptions);
  }

  public string JoinPaths(char separator, params string[] paths)
  {
    var builder = new StringBuilder();

    for (int i = 0; i < paths.Length; i++)
    {
      string? path = paths[i];
      if (string.IsNullOrEmpty(path))
      {
        continue;
      }

      if (builder.Length == 0)
      {
        builder.Append(path);
        continue;
      }

      if (builder[^1] != separator && path[0] != separator)
      {
        builder.Append(separator);
      }

      builder.Append(path);
    }

    return builder.ToString();
  }

  public void MoveFile(string sourceFile, string destinationFile, bool overwrite)
  {
    File.Move(sourceFile, destinationFile, overwrite);
  }

  public FileStream OpenFileStream(string path, FileMode mode, FileAccess access)
  {
    return File.Open(path, mode, access);
  }

  public FileStream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare fileShare)
  {
    return File.Open(path, mode, access, fileShare);
  }

  public async Task<byte[]> ReadAllBytesAsync(string path)
  {
    return await File.ReadAllBytesAsync(path);
  }

  public async Task<string[]> ReadAllLinesAsync(string path)
  {
    return await File.ReadAllLinesAsync(path);
  }

  public string ReadAllText(string filePath)
  {
    return File.ReadAllText(filePath);
  }

  public Task<string> ReadAllTextAsync(string path)
  {
    return File.ReadAllTextAsync(path);
  }

  public async Task ReplaceLineInFile(string filePath, string matchPattern, string replaceLineWith, int maxMatches = -1)
  {
    var lines = await File.ReadAllLinesAsync(filePath);
    var matchCount = 0;
    for (var i = 0; i < lines.Length; i++)
    {
      if (lines[i].Contains(matchPattern, StringComparison.OrdinalIgnoreCase))
      {
        lines[i] = replaceLineWith;
        matchCount++;
      }
      if (maxMatches > -1 && matchCount >= maxMatches)
      {
        break;
      }
    }
    await File.WriteAllLinesAsync(filePath, lines);
  }

  public async Task<Result<string>> ResolveFilePath(string fileName)
  {
    try
    {
      var psi = new ProcessStartInfo
      {
        FileName = OperatingSystem.IsWindows() ? "where.exe" : "which",
        Arguments = fileName,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(psi);
      if (process == null)
      {
        return Result
          .Fail<string>($"Failed to start process to resolve file path for file name '{fileName}'.")
          .Log(_logger);
      }

      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
      var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
      if (string.IsNullOrWhiteSpace(output))
      {
        var errorOutput = await process.StandardError.ReadToEndAsync(cts.Token);
        return Result
          .Fail<string>($"File '{fileName}' not found. Error: {errorOutput}")
          .Log(_logger);
      }

      output = output.Trim();
      var firstItem = output
        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault();

      if (string.IsNullOrWhiteSpace(firstItem) || !FileExists(firstItem))
      {
        return Result
          .Fail<string>($"File '{fileName}' not found.")
          .Log(_logger);
      }

      return Result.Ok(firstItem);
    }
    catch (OperationCanceledException)
    {
      return Result
        .Fail<string>($"Timed out while resolving file path for file name '{fileName}'.")
        .Log(_logger);
    }
    catch (Exception ex)
    {
      return Result
        .Fail<string>(ex, $"Failed to resolve file path for file name '{fileName}'.")
        .Log(_logger);
    }
  }

  [SupportedOSPlatform("linux")]
  [SupportedOSPlatform("macos")]
  public void SetUnixFileMode(string filePath, UnixFileMode fileMode)
  {
    File.SetUnixFileMode(filePath, fileMode);
  }

  public Task WriteAllBytesAsync(string path, byte[] buffer, CancellationToken cancellationToken = default)
  {
    return File.WriteAllBytesAsync(path, buffer, cancellationToken);
  }

  public Task WriteAllLines(string path, List<string> lines)
  {
    return File.WriteAllLinesAsync(path, lines);
  }

  public void WriteAllText(string filePath, string contents)
  {
    File.WriteAllText(filePath, contents);
  }

  public Task WriteAllTextAsync(string path, string content)
  {
    return File.WriteAllTextAsync(path, content);
  }
}