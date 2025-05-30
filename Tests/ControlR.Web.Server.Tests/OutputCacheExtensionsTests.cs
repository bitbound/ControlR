using ControlR.Web.Server.Extensions;
using Microsoft.AspNetCore.OutputCaching;
using Moq;
using Xunit.Abstractions;

namespace ControlR.Web.Server.Tests;

public class OutputCacheExtensionsTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task InvalidateDeviceGridCacheAsync_EvictsByDeviceGridTag()
  {
    // Arrange
    var mockOutputCacheStore = new Mock<IOutputCacheStore>();
    mockOutputCacheStore
      .Setup(s => s.EvictByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(ValueTask.CompletedTask);

    // Act
    await mockOutputCacheStore.Object.InvalidateDeviceGridCacheAsync();

    // Assert
    mockOutputCacheStore.Verify(s => s.EvictByTagAsync("device-grid", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task InvalidateDeviceCacheAsync_EvictsByDeviceSpecificTag()
  {
    // Arrange
    var mockOutputCacheStore = new Mock<IOutputCacheStore>();
    mockOutputCacheStore
      .Setup(s => s.EvictByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(ValueTask.CompletedTask);

    var deviceId = Guid.NewGuid();

    // Act
    await mockOutputCacheStore.Object.InvalidateDeviceCacheAsync(deviceId);

    // Assert
    mockOutputCacheStore.Verify(s => s.EvictByTagAsync($"device-{deviceId}", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task InvalidateUserDeviceGridCacheAsync_EvictsByUserTag()
  {
    // Arrange
    var mockOutputCacheStore = new Mock<IOutputCacheStore>();
    mockOutputCacheStore
      .Setup(s => s.EvictByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(ValueTask.CompletedTask);

    var userId = Guid.NewGuid().ToString();

    // Act
    await mockOutputCacheStore.Object.InvalidateUserDeviceGridCacheAsync(userId);

    // Assert
    mockOutputCacheStore.Verify(s => s.EvictByTagAsync($"user-{userId}", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task InvalidateDeviceGridRequestCacheAsync_EvictsByRequestHashTag()
  {
    // Arrange
    var mockOutputCacheStore = new Mock<IOutputCacheStore>();
    mockOutputCacheStore
      .Setup(s => s.EvictByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(ValueTask.CompletedTask);

    var requestHash = "abc123";

    // Act
    await mockOutputCacheStore.Object.InvalidateDeviceGridRequestCacheAsync(requestHash);

    // Assert
    mockOutputCacheStore.Verify(s => s.EvictByTagAsync($"request-{requestHash}", It.IsAny<CancellationToken>()), Times.Once);
  }
}
