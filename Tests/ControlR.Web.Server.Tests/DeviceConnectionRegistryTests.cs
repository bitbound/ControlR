using ControlR.Web.Server.Services.DeviceManagement;

namespace ControlR.Web.Server.Tests;

public class DeviceConnectionRegistryTests
{
  [Fact]
  public async Task GetConnectionId_UnknownDevice_ReturnsFailure()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var registry = new DeviceConnectionRegistry();

    var result = await registry.GetConnectionId(Guid.NewGuid(), cancellationToken);

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task Remove_DoesNotEvictEntryOfNewerConnection()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var deviceId = Guid.NewGuid();
    var registry = new DeviceConnectionRegistry();
    await registry.Set(deviceId, "conn-new", cancellationToken);
    await registry.Remove(deviceId, "conn-old", cancellationToken);

    var result = await registry.GetConnectionId(deviceId, cancellationToken);

    Assert.True(result.IsSuccess);
    Assert.Equal("conn-new", result.Value);
  }

  [Fact]
  public async Task Remove_EvictsMatchingConnection()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var deviceId = Guid.NewGuid();
    var registry = new DeviceConnectionRegistry();
    await registry.Set(deviceId, "conn-1", cancellationToken);

    await registry.Remove(deviceId, "conn-1", cancellationToken);

    var result = await registry.GetConnectionId(deviceId, cancellationToken);

    Assert.False(result.IsSuccess);
  }

  [Fact]
  public async Task Set_MakesConnectionIdResolvable()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var deviceId = Guid.NewGuid();
    var registry = new DeviceConnectionRegistry();

    await registry.Set(deviceId, "conn-1", cancellationToken);

    var result = await registry.GetConnectionId(deviceId, cancellationToken);

    Assert.True(result.IsSuccess);
    Assert.Equal("conn-1", result.Value);
  }
}
