using ControlR.Web.Server.Services.DeviceManagement;

namespace ControlR.Web.Server.Tests;

public class DeviceConnectionRegistryTests
{
  [Fact]
  public void Remove_DoesNotEvictEntryOfNewerConnection()
  {
    var registry = new DeviceConnectionRegistry();
    registry.Set(Guid.NewGuid(), "conn-old");
    var deviceId = Guid.NewGuid();
    registry.Set(deviceId, "conn-new");
    registry.Remove(deviceId, "conn-old");

    Assert.True(registry.TryGetConnectionId(deviceId, out var connectionId));
    Assert.Equal("conn-new", connectionId);
  }

  [Fact]
  public void Remove_EvictsMatchingConnection()
  {
    var deviceId = Guid.NewGuid();
    var registry = new DeviceConnectionRegistry();
    registry.Set(deviceId, "conn-1");

    registry.Remove(deviceId, "conn-1");

    Assert.False(registry.TryGetConnectionId(deviceId, out _));
  }

  [Fact]
  public void Set_MakesConnectionIdResolvable()
  {
    var deviceId = Guid.NewGuid();
    var registry = new DeviceConnectionRegistry();

    registry.Set(deviceId, "conn-1");

    Assert.True(registry.TryGetConnectionId(deviceId, out var connectionId));
    Assert.Equal("conn-1", connectionId);
  }

  [Fact]
  public void TryGetConnectionId_UnknownDevice_ReturnsFalse()
  {
    var registry = new DeviceConnectionRegistry();

    Assert.False(registry.TryGetConnectionId(Guid.NewGuid(), out var connectionId));
    Assert.Null(connectionId);
  }
}
