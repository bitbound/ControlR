using System.Text;
using ControlR.Libraries.TestingUtilities.FileSystem;

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

    var content = await fileSystem.ReadAllTextAsync("/data/test.txt");

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