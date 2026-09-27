using ControlR.Viewer.Avalonia.ViewModels;
using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Viewer.Avalonia.Tests.ViewModels;

public class LogFilesTreeItemViewModelTests
{
  [Fact]
  public void Children_InitializedEmpty()
  {
    // Arrange & Act
    var node = new LogFilesTreeItemViewModel("test", LogKind.Agent, null, isFile: true);

    // Assert
    Assert.NotNull(node.Children);
    Assert.Empty(node.Children);
  }

  [Fact]
  public void Constructor_WithoutSelector_IsFileFalse()
  {
    // Arrange & Act
    var node = new LogFilesTreeItemViewModel("MyGroup", kind: null, username: null, isFile: false);

    // Assert
    Assert.False(node.IsFile);
    Assert.Equal("MyGroup", node.Name);
    Assert.Null(node.Kind);
    Assert.Null(node.Username);
  }

  [Fact]
  public void Constructor_WithSelector_IsFileTrue()
  {
    // Arrange & Act
    var node = new LogFilesTreeItemViewModel("LogFile.log", LogKind.Agent, null, isFile: true);

    // Assert
    Assert.True(node.IsFile);
    Assert.Equal("LogFile.log", node.Name);
    Assert.Equal(LogKind.Agent, node.Kind);
    Assert.Null(node.Username);
  }

  [Fact]
  public void IsExpanded_DefaultsFalse()
  {
    // Arrange & Act
    var node = new LogFilesTreeItemViewModel("test", LogKind.Agent, null, isFile: true);

    // Assert
    Assert.False(node.IsExpanded);
  }
}
