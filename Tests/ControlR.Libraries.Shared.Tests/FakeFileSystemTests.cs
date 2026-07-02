using System.Text;
using ControlR.Libraries.Shared.Services.FileSystem;
using ControlR.Libraries.TestingUtilities.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Libraries.Shared.Tests;

public class FakeFileSystemTests
{
  [Fact]
  public void DeleteFile_MissingPathDoesNotThrow()
  {
    var fileSystem = new FakeFileSystem();

    fileSystem.DeleteFile("/data/missing.txt");

    Assert.False(fileSystem.FileExists("/data/missing.txt"));
  }

  [Fact]
  public void GetDirectoryInfo_ReturnsMissingDirectoryMetadata()
  {
    var fileSystem = new FakeFileSystem();

    var directoryInfo = fileSystem.GetDirectoryInfo("/data/missing");

    Assert.False(directoryInfo.Exists);
    Assert.Equal("missing", directoryInfo.Name);
    Assert.Equal("/data/missing", directoryInfo.FullName);
  }

  [Fact]
  public void GetDrives_ReturnsConfiguredDriveMetadata()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddDrive(
      "/mnt/storage",
      name: "storage",
      driveFormat: "ext4",
      totalSize: 100,
      totalFreeSpace: 25,
      volumeLabel: "Data");

    var drive = Assert.Single(fileSystem.GetDrives());

    Assert.Equal("storage", drive.Name);
    Assert.Equal("/mnt/storage", drive.RootDirectory.FullName);
    Assert.Equal("ext4", drive.DriveFormat);
    Assert.Equal(100, drive.TotalSize);
    Assert.Equal(25, drive.TotalFreeSpace);
    Assert.Equal("Data", drive.VolumeLabel);
  }

  [Fact]
  public void GetFileInfo_ReturnsMetadataForMissingFilePath()
  {
    var fileSystem = new FakeFileSystem();

    var fileInfo = fileSystem.GetFileInfo("/data/missing.txt");

    Assert.Equal("missing.txt", fileInfo.Name);
    Assert.Equal("/data/missing.txt", fileInfo.FullName);
    Assert.Equal(0, fileInfo.Length);
  }

  [Fact]
  public void GetFiles_FiltersByPattern()
  {
    var fileSystem = new FakeFileSystem();

    fileSystem.AddFile("/logs/LogFile-1.log", "a");
    fileSystem.AddFile("/logs/LogFile-2.log", "b");
    fileSystem.AddFile("/logs/notes.txt", "c");

    var files = fileSystem.GetFiles("/logs", "LogFile*.log");

    Assert.Equal(2, files.Length);
    Assert.DoesNotContain("/logs/notes.txt", files);
  }

  [Fact]
  public void GetFiles_UsesCaseSensitivePatternsWhenConfigured()
  {
    var fileSystem = new FakeFileSystem(isCaseSensitive: true);
    fileSystem.AddFile("/logs/Agent.log", "a");

    var files = fileSystem.GetFiles("/logs", "agent*.log");

    Assert.Empty(files);
  }

  [Fact]
  public void JoinPaths_RemovesDuplicateSeparatorsWhenSegmentsContainLeadingTrailingSeparators()
  {
    var fileSystem = new FileSystem(NullLogger<FileSystem>.Instance);

    var path = fileSystem.JoinPaths('/', "/tmp/", "/ControlR_Update/", "installer");

    Assert.Equal("/tmp/ControlR_Update/installer", path);
  }

  [Fact]
  public void MoveDirectory_CreatesDestinationParentHierarchy()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile("/src/file.txt", "content");

    fileSystem.MoveDirectory("/src", "/deep/nested/dst");

    Assert.True(fileSystem.DirectoryExists("/deep/nested/dst"));
    Assert.True(fileSystem.FileExists("/deep/nested/dst/file.txt"));
    Assert.False(fileSystem.FileExists("/src/file.txt"));
  }

  [Fact]
  public void MoveDirectory_MovesFilesToNewPath()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile("/src/file1.txt", "content1");
    fileSystem.AddFile("/src/file2.txt", "content2");

    fileSystem.MoveDirectory("/src", "/dst");

    Assert.False(fileSystem.DirectoryExists("/src"));
    Assert.True(fileSystem.DirectoryExists("/dst"));
    Assert.True(fileSystem.FileExists("/dst/file1.txt"));
    Assert.True(fileSystem.FileExists("/dst/file2.txt"));
    Assert.False(fileSystem.FileExists("/src/file1.txt"));
    Assert.False(fileSystem.FileExists("/src/file2.txt"));
  }

  [Fact]
  public async Task MoveDirectory_MovesNestedDirectoriesAndFiles()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile("/src/sub/deep/file.txt", "deep-content");

    fileSystem.MoveDirectory("/src", "/dst");

    Assert.False(fileSystem.DirectoryExists("/src"));
    Assert.False(fileSystem.DirectoryExists("/src/sub"));
    Assert.True(fileSystem.DirectoryExists("/dst/sub/deep"));
    Assert.True(fileSystem.FileExists("/dst/sub/deep/file.txt"));

    var content = await fileSystem.ReadAllTextAsync("/dst/sub/deep/file.txt", TestContext.Current.CancellationToken);
    Assert.Equal("deep-content", content);
  }

  [Fact]
  public void MoveDirectory_PreservesDirectoryAttributes()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddDirectory("/src", attributes: FileAttributes.Directory | FileAttributes.Hidden);

    fileSystem.MoveDirectory("/src", "/dst");

    var dirInfo = fileSystem.GetDirectoryInfo("/dst");
    Assert.True(dirInfo.Exists);
    Assert.Equal(FileAttributes.Directory | FileAttributes.Hidden, dirInfo.Attributes);
  }

  [Fact]
  public async Task MoveDirectory_PreservesFileContent()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile("/src/data.bin", "binary-content");

    fileSystem.MoveDirectory("/src", "/dst");

    var content = await fileSystem.ReadAllTextAsync("/dst/data.bin", TestContext.Current.CancellationToken);
    Assert.Equal("binary-content", content);
  }

  [Fact]
  public void MoveDirectory_ThrowsWhenDestinationAlreadyExists()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile("/src/file.txt", "content");
    fileSystem.AddDirectory("/dst");

    var ex = Assert.Throws<IOException>(() =>
      fileSystem.MoveDirectory("/src", "/dst"));

    Assert.Contains("/dst", ex.Message);
  }

  [Fact]
  public void MoveDirectory_ThrowsWhenSourceDoesNotExist()
  {
    var fileSystem = new FakeFileSystem();

    var ex = Assert.Throws<DirectoryNotFoundException>(() =>
      fileSystem.MoveDirectory("/nonexistent", "/dst"));

    Assert.Contains("/nonexistent", ex.Message);
  }

  [Fact]
  public void OpenFileStream_AllowsSharedReads()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile("/data/shared.txt", "content");

    using var firstHandle = fileSystem.OpenFileStream("/data/shared.txt", FileMode.Open, FileAccess.Read, FileShare.Read);
    using var secondHandle = fileSystem.OpenFileStream("/data/shared.txt", FileMode.Open, FileAccess.Read, FileShare.Read);

    Assert.NotNull(secondHandle);
  }

  [Fact]
  public void OpenFileStream_AppendRequiresWriteOnlyAccess()
  {
    var fileSystem = new FakeFileSystem();

    var exception = Assert.Throws<ArgumentException>(() =>
      fileSystem.OpenFileStream("/data/append.txt", FileMode.Append, FileAccess.ReadWrite, FileShare.None));

    Assert.Equal("access", exception.ParamName);
  }

  [Fact]
  public async Task OpenFileStream_PersistsWrittenContent()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddDirectory("/data");

    await using (var stream = fileSystem.OpenFileStream("/data/test.txt", FileMode.Create, FileAccess.Write, FileShare.None))
    {
      var bytes = Encoding.UTF8.GetBytes("hello world");
      await stream.WriteAsync(bytes, TestContext.Current.CancellationToken);
    }

    var content = await fileSystem.ReadAllTextAsync("/data/test.txt", TestContext.Current.CancellationToken);

    Assert.Equal("hello world", content);
  }

  [Fact]
  public void OpenFileStream_RespectsExclusiveSharing()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile("/data/locked.txt", "content");

    using var firstHandle = fileSystem.OpenFileStream("/data/locked.txt", FileMode.Open, FileAccess.Read, FileShare.None);

    var exception = Assert.Throws<IOException>(() =>
      fileSystem.OpenFileStream("/data/locked.txt", FileMode.Open, FileAccess.Read, FileShare.Read));

    Assert.Contains("being used by another process", exception.Message);
  }

  [Fact]
  public void OpenFileStream_WriteOnlyHandleRejectsReads()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile("/data/write-only.txt", "content");

    using var stream = fileSystem.OpenFileStream("/data/write-only.txt", FileMode.Open, FileAccess.Write, FileShare.None);

    Assert.False(stream.CanRead);
    Assert.Throws<NotSupportedException>(() => stream.ReadByte());
  }

  [Fact]
  public async Task ReplaceLineInFile_ReplacesMatchingLine()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.AddFile("/data/settings.txt", "alpha\nbeta\ngamma");

    await fileSystem.ReplaceLineInFile("/data/settings.txt", "beta", "delta");

    var lines = await fileSystem.ReadAllLinesAsync("/data/settings.txt");

    Assert.Equal(["alpha", "delta", "gamma"], lines);
  }

  [Fact]
  public async Task ResolveFilePath_ReturnsConfiguredResult()
  {
    var fileSystem = new FakeFileSystem();
    fileSystem.SetResolvedFilePath("controlr", "/usr/bin/controlr");

    var result = await fileSystem.ResolveFilePath("controlr");
    var missing = await fileSystem.ResolveFilePath("missing");

    Assert.True(result.IsSuccess);
    Assert.Equal("/usr/bin/controlr", result.Value);
    Assert.False(missing.IsSuccess);
  }

  [Fact]
  public void WindowsPaths_AreNormalizedCaseInsensitively()
  {
    var fileSystem = new FakeFileSystem('\\');
    fileSystem.AddFile(@"C:\Logs\Agent.log", "content");

    Assert.True(fileSystem.FileExists(@"c:\logs\agent.log"));
    Assert.Equal(@"C:\Logs\Agent.log", fileSystem.GetFiles(@"C:\Logs").Single());
  }
}