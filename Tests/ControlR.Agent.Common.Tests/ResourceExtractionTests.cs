using ControlR.Libraries.Shared.Services;
using ControlR.Agent.Common.Services;
using System.Reflection;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Agent.Common.Tests;

public class ResourceExtractionTests
{
  private readonly EmbeddedResourceAccessor _accessor = new();
  private readonly Assembly _assembly = typeof(AgentUpdater).Assembly;

  [Fact]
  public async Task GetResourceAsString_ForLaunchAgent_ReturnsExpectedString()
  {
    // Arrange
    var solutionDirResult = IoHelper.GetSolutionDir(Directory.GetCurrentDirectory());
    Assert.True(solutionDirResult.IsSuccess);
    var resourcePath = Path.Combine(solutionDirResult.Value, "ControlR.Agent.Common", "Resources", "LaunchAgent.plist");
    var expected = await File.ReadAllTextAsync(resourcePath);

    // Act
    var actual = await _accessor.GetResourceAsString(_assembly, "LaunchAgent.plist");

    // Assert
    Assert.Equal(expected, actual);
  }

  [Fact]
  public async Task GetResourceAsString_ForLaunchDaemon_ReturnsExpectedString()
  {
    // Arrange
    var solutionDirResult = IoHelper.GetSolutionDir(Directory.GetCurrentDirectory());
    Assert.True(solutionDirResult.IsSuccess);
    var resourcePath =
      Path.Combine(solutionDirResult.Value, "ControlR.Agent.Common", "Resources", "LaunchDaemon.plist");
    var expected = await File.ReadAllTextAsync(resourcePath);
    // Act
    var actual = await _accessor.GetResourceAsString(_assembly, "LaunchDaemon.plist");
    // Assert
    Assert.Equal(expected, actual);
  }

  [Fact]
  public async Task GetResourceAsString_ForLinuxAgentService_ReturnsExpectedString()
  {
    // Arrange
    var solutionDirResult = IoHelper.GetSolutionDir(Directory.GetCurrentDirectory());
    Assert.True(solutionDirResult.IsSuccess);
    var resourcePath = Path.Combine(solutionDirResult.Value, "ControlR.Agent.Common", "Resources",
      "controlr.agent.service");
    var expected = await File.ReadAllTextAsync(resourcePath);

    // Act
    var actual =
      await _accessor.GetResourceAsString(_assembly, "ControlR.Agent.Common.Resources.controlr.agent.service");

    // Assert
    Assert.Equal(expected, actual);
  }

  [Fact]
  public async Task GetResourceAsString_ForLinuxDesktopService_ReturnsExpectedString()
  {
    // Arrange
    var solutionDirResult = IoHelper.GetSolutionDir(Directory.GetCurrentDirectory());
    Assert.True(solutionDirResult.IsSuccess);
    var resourcePath = Path.Combine(solutionDirResult.Value, "ControlR.Agent.Common", "Resources", "controlr.desktop.service");
    var expected = await File.ReadAllTextAsync(resourcePath);

    // Act
    var actual =
      await _accessor.GetResourceAsString(_assembly, "ControlR.Agent.Common.Resources.controlr.desktop.service");

    // Assert
    Assert.Equal(expected, actual);
  }
}