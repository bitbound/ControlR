using System.Diagnostics;

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

    bool FileExists(string path);
    string[] GetDirectories(string path);
    string[] GetFiles(string path);

    public string[] GetFiles(string path, string searchPattern);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption);

    public string[] GetFiles(string path, string searchPattern, EnumerationOptions enumerationOptions);

    FileVersionInfo GetFileVersionInfo(string filePath);

    void MoveFile(string sourceFile, string destinationFile, bool overwrite);

    FileStream OpenFileStream(string path, FileMode mode, FileAccess access);
    FileStream OpenFileStream(string path, FileMode mode, FileAccess access, FileShare fileShare);

    Task<byte[]> ReadAllBytesAsync(string path);

    Task<string[]> ReadAllLinesAsync(string path);

    string ReadAllText(string filePath);

    Task<string> ReadAllTextAsync(string path);

    Task ReplaceLineInFile(string filePath, string matchPattern, string replaceLineWith, int maxMatches = -1);

    Task WriteAllBytesAsync(string path, byte[] buffer, CancellationToken cancellationToken = default);

    Task WriteAllLines(string path, List<string> lines);

    void WriteAllText(string filePath, string contents);

    Task WriteAllTextAsync(string path, string content);
}

public class FileSystem : IFileSystem
{
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

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string[] GetDirectories(string path)
    {
        return Directory.GetDirectories(path);
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

    public FileVersionInfo GetFileVersionInfo(string filePath)
    {
        return FileVersionInfo.GetVersionInfo(filePath);
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