using ControlR.Libraries.Shared.Services.FileSystem;

namespace ControlR.Libraries.TestingUtilities.FileSystem;

public sealed class FakeFileSystemDirectoryInfo(string fullName) : IFileSystemDirectory
{
  public FileAttributes Attributes { get; init; } = FileAttributes.Directory;

  public bool Exists { get; init; } = true;

  public string FullName { get; init; } = fullName;

  public DateTime LastWriteTime { get; init; } = DateTime.UtcNow;

  public string Name { get; init; } = Path.GetFileName(fullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

  public IFileSystemDirectory? Parent { get; init; }
}

public sealed class FakeFileSystemFileInfo(string fullName) : IFileSystemFile
{
  public FileAttributes Attributes { get; init; } = FileAttributes.Normal;

  public string FullName { get; init; } = fullName;

  public DateTime LastWriteTime { get; init; } = DateTime.UtcNow;

  public long Length { get; init; }

  public string Name { get; init; } = Path.GetFileName(fullName);
}

public sealed class FakeFileSystemDriveInfo(string name, string rootPath) : IFileSystemDrive
{
  public string DriveFormat { get; init; } = string.Empty;

  public DriveType DriveType { get; init; } = DriveType.Fixed;

  public bool IsReady { get; init; } = true;

  public string Name { get; init; } = name;

  public IFileSystemDirectory RootDirectory { get; init; } = new FakeFileSystemDirectoryInfo(rootPath);

  public long TotalFreeSpace { get; init; }

  public long TotalSize { get; init; }

  public string VolumeLabel { get; init; } = string.Empty;
}