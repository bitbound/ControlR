using ControlR.Viewer.Avalonia.ViewModels;

namespace ControlR.Viewer.Avalonia.Tests.ViewModels;

public class LogFilesTreeItemViewModelTests
{
  [Fact]
  public void Children_InitializedEmpty()
  {
    // Arrange & Act
    var node = new LogFilesTreeItemViewModel("test", "/test", isFile: true);

    // Assert
    Assert.NotNull(node.Children);
    Assert.Empty(node.Children);
  }

  [Fact]
  public void Constructor_WithFileName_IsFileTrue()
  {
    // Arrange & Act
    var node = new LogFilesTreeItemViewModel("log.txt", "/var/log/log.txt", isFile: true);

    // Assert
    Assert.True(node.IsFile);
    Assert.Equal("log.txt", node.Name);
    Assert.Equal("/var/log/log.txt", node.FullPath);
  }

  [Fact]
  public void Constructor_WithFullPath_IsFileFalse()
  {
    // Arrange & Act
    var node = new LogFilesTreeItemViewModel("MyGroup", fullPath: null, isFile: false);

    // Assert
    Assert.False(node.IsFile);
    Assert.Equal("MyGroup", node.Name);
    Assert.Null(node.FullPath);
  }

  [Fact]
  public void IsExpanded_DefaultsFalse()
  {
    // Arrange & Act
    var node = new LogFilesTreeItemViewModel("test", "/test", isFile: true);

    // Assert
    Assert.False(node.IsExpanded);
  }
}
