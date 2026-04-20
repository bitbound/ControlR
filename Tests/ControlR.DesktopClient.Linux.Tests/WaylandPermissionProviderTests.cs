using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Linux.Services;
using ControlR.DesktopClient.Linux.XdgPortal;
using ControlR.Libraries.Shared.Services.FileSystem;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace ControlR.DesktopClient.Linux.Tests;

public class WaylandPermissionProviderTests
{
  [Fact]
  public async Task RequestRemoteControlPermission_UsesInteractivePortalRequestWithoutInitializingCapture()
  {
    var timeProvider = new FakeTimeProvider();
    
    var fakePortal = new FakeXdgDesktopPortal
    {
      RequestRemoteDesktopPermissionResult = true,
      ProbeResult = true
    };

    var portalFactory = new Mock<IXdgDesktopPortalFactory>();
    portalFactory
      .Setup(x => x.CreateNew(It.IsAny<bool>()))
      .Returns(fakePortal);

    var fileSystem = new Mock<IFileSystem>();
    fileSystem
      .Setup(x => x.FileExists(It.IsAny<string>()))
      .Returns(true);
    fileSystem
      .Setup(x => x.ReadAllText(It.IsAny<string>()))
      .Returns("restore-token");

    var options = new Mock<IOptionsMonitor<DesktopClientOptions>>();
    options
      .SetupGet(x => x.CurrentValue)
      .Returns(new DesktopClientOptions { InstanceId = "instance-id" });

    var sut = new WaylandPermissionProvider(
      timeProvider,
      fileSystem.Object,
      portalFactory.Object,
      options.Object,
      NullLogger<WaylandPermissionProvider>.Instance);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    await sut.RequestRemoteControlPermission(bypassRestoreToken: true, cancellationToken: cts.Token);

    Assert.Equal(1, fakePortal.RequestRemoteDesktopPermissionCallCount);
    Assert.Equal(0, fakePortal.InitializeCallCount);
  }
}