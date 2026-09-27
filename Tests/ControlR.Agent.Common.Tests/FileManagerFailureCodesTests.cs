using ControlR.Agent.Common.Services.FileManager;
using ControlR.Agent.Shared.Services;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Shared.Services;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Agent.Common.Tests;

/// <summary>
/// Pins the machine-readable failure code the agent attaches to each file-system refusal (#247).
/// </summary>
public class FileManagerFailureCodesTests
{
  [Fact]
  public async Task CreateDirectory_WhenDirectoryAlreadyExists_ReportsAlreadyExists()
  {
    var fileSystem = new Mock<IFileSystem>();
    fileSystem.Setup(x => x.DirectoryExists("/base/new-dir")).Returns(true);
    var fileManager = CreateManager(fileSystem.Object);

    var result = await fileManager.CreateDirectory("/base/new-dir");

    Assert.False(result.IsSuccess);
    Assert.Equal(OperationFailureCode.AlreadyExists, result.Code);
  }

  [Fact]
  public async Task CreateDirectory_WhenPathIsBlank_ReportsInvalidInput()
  {
    var fileManager = CreateManager(new Mock<IFileSystem>().Object);

    var result = await fileManager.CreateDirectory(string.Empty);

    Assert.False(result.IsSuccess);
    Assert.Equal(OperationFailureCode.InvalidInput, result.Code);
  }

  [Fact]
  public async Task DeleteFileSystemEntry_WhenPathDoesNotExist_ReportsNotFound()
  {
    var fileSystem = new Mock<IFileSystem>();
    fileSystem.Setup(x => x.DirectoryExists("/base/gone")).Returns(false);
    fileSystem.Setup(x => x.FileExists("/base/gone")).Returns(false);
    var fileManager = CreateManager(fileSystem.Object);

    var result = await fileManager.DeleteFileSystemEntry("/base/gone");

    Assert.False(result.IsSuccess);
    Assert.Equal(OperationFailureCode.NotFound, result.Code);
  }

  private static FileManager CreateManager(IFileSystem fileSystem)
  {
    return new FileManager(
      fileSystem,
      new Mock<IFileSystemPathProvider>().Object,
      new Mock<ISystemEnvironment>().Object,
      NullLogger<FileManager>.Instance);
  }
}
