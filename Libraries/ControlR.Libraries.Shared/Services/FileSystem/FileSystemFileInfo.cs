namespace ControlR.Libraries.Shared.Services.FileSystem;

public interface IFileSystemFile
{
  FileAttributes Attributes { get; }
  string FullName { get; }
  DateTime LastWriteTime { get; }
  long Length { get; }
  string Name { get; }
}

internal sealed class FileSystemFileInfo(FileInfo fileInfo) : IFileSystemFile
{
  public FileAttributes Attributes => fileInfo.Attributes;

  public string FullName => fileInfo.FullName;

  public DateTime LastWriteTime => fileInfo.LastWriteTime;

  public long Length => fileInfo.Length;

  public string Name => fileInfo.Name;
}

