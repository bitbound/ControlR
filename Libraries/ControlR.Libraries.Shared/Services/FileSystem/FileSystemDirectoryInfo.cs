namespace ControlR.Libraries.Shared.Services.FileSystem;

public interface IFileSystemDirectory
{
  FileAttributes Attributes { get; }
  bool Exists { get; }
  string FullName { get; }
  DateTime LastWriteTime { get; }
  string Name { get; }
  IFileSystemDirectory? Parent { get; }
}


internal sealed class FileSystemDirectoryInfo(DirectoryInfo directoryInfo) : IFileSystemDirectory
{
  public FileAttributes Attributes => directoryInfo.Attributes;

  public bool Exists => directoryInfo.Exists;

  public string FullName => directoryInfo.FullName;

  public DateTime LastWriteTime => directoryInfo.LastWriteTime;

  public string Name => directoryInfo.Name;

  public IFileSystemDirectory? Parent => directoryInfo.Parent is null
    ? null
    : new FileSystemDirectoryInfo(directoryInfo.Parent);
}

