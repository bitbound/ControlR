using ControlR.Web.Server.Services.DeviceManagement;

namespace ControlR.Web.Server.Tests;

public class DeviceConnectionRegistryTests
{
  [Fact]
  public async Task GetConnectionId_UnknownDevice_ReturnsNull()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var registry = new DeviceConnectionRegistry();

    Assert.Null(await registry.GetConnectionId(Guid.NewGuid(), cancellationToken));
  }

  [Fact]
  public async Task Remove_DoesNotEvictEntryOfNewerConnection()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var deviceId = Guid.NewGuid();
    var registry = new DeviceConnectionRegistry();
    await registry.Set(deviceId, "conn-new", cancellationToken);
    await registry.Remove(deviceId, "conn-old", cancellationToken);

    Assert.Equal("conn-new", await registry.GetConnectionId(deviceId, cancellationToken));
  }

  [Fact]
  public async Task Remove_EvictsMatchingConnection()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var deviceId = Guid.NewGuid();
    var registry = new DeviceConnectionRegistry();
    await registry.Set(deviceId, "conn-1", cancellationToken);

    await registry.Remove(deviceId, "conn-1", cancellationToken);

    Assert.Null(await registry.GetConnectionId(deviceId, cancellationToken));
  }

  [Fact]
  public async Task Set_MakesConnectionIdResolvable()
  {
    var cancellationToken = TestContext.Current.CancellationToken;
    var deviceId = Guid.NewGuid();
    var registry = new DeviceConnectionRegistry();

    await registry.Set(deviceId, "conn-1", cancellationToken);

    Assert.Equal("conn-1", await registry.GetConnectionId(deviceId, cancellationToken));
  }
}
