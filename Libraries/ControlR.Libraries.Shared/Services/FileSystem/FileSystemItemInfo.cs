namespace ControlR.Libraries.Shared.Services.FileSystem;

public interface IFileSystemItemInfo
{
  FileAttributes Attributes { get; }
  DateTime CreationTime { get; }
  bool Exists { get; }
  string FullName { get; }
  DateTime LastWriteTime { get; }
  string Name { get; }
}

internal sealed class FileSystemItemInfo(System.IO.FileSystemInfo fileSystemInfo) : IFileSystemItemInfo
{
  public FileAttributes Attributes => fileSystemInfo.Attributes;

  public DateTime CreationTime => fileSystemInfo.CreationTime;

  public bool Exists => fileSystemInfo.Exists;

  public string FullName => fileSystemInfo.FullName;

  public DateTime LastWriteTime => fileSystemInfo.LastWriteTime;

  public string Name => fileSystemInfo.Name;
}
