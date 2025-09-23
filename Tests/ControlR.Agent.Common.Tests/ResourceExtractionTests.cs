using ControlR.Libraries.Shared.Services;
using ControlR.Agent.Common.Services;
using System.Reflection;
using ControlR.Tests.TestingUtilities;

namespace ControlR.Agent.Common.Tests;

public class ResourceExtractionTests
{
  private readonly EmbeddedResourceAccessor _accessor;
  private readonly Assembly _assembly;

  public ResourceExtractionTests()
  {
    _accessor = new EmbeddedResourceAccessor();
    _assembly = typeof(AgentUpdater).Assembly;
  }

  [Fact]
  public async Task GetResourceAsString_ForLaunchAgent_ReturnsExpectedString()
  {
    // Arrange
    var solutionDir = IoHelper.GetSolutionDir(Directory.GetCurrentDirectory());
    var resourcePath = Path.Combine(solutionDir, "ControlR.Agent.Common", "Resources", "LaunchAgent.plist");
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
    var solutionDir = IoHelper.GetSolutionDir(Directory.GetCurrentDirectory());
    var resourcePath = Path.Combine(solutionDir, "ControlR.Agent.Common", "Resources", "LaunchDaemon.plist");
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
    var solutionDir = IoHelper.GetSolutionDir(Directory.GetCurrentDirectory());
    var resourcePath = Path.Combine(solutionDir, "ControlR.Agent.Common", "Resources", "controlr.agent.service");
    var expected = await File.ReadAllTextAsync(resourcePath);

    // Act
    var actual = await _accessor.GetResourceAsString(_assembly, "ControlR.Agent.Common.Resources.controlr.agent.service");

    // Assert
    Assert.Equal(expected, actual);
  }

  [Fact]
  public async Task GetResourceAsString_ForLinuxDesktopService_ReturnsExpectedString()
  {
    // Arrange
    var solutionDir = IoHelper.GetSolutionDir(Directory.GetCurrentDirectory());
    var resourcePath = Path.Combine(solutionDir, "ControlR.Agent.Common", "Resources", "controlr.desktop.service");
    var expected = await File.ReadAllTextAsync(resourcePath);

    // Act
    var actual = await _accessor.GetResourceAsString(_assembly, "ControlR.Agent.Common.Resources.controlr.desktop.service");

    // Assert
    Assert.Equal(expected, actual);
  }
}
