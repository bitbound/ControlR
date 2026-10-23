using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.DesktopClient.Common.Tests;

public class DesktopCapturerFactoryTests
{
  [Fact]
  public void CreateNew_WhenEncoderTypeIsVpx_ThrowsNotSupportedException()
  {
    var provider = new ServiceCollection().BuildServiceProvider();
    var options = Microsoft.Extensions.Options.Options.Create(new RemoteControlSessionOptions { EncoderType = CaptureEncoderType.Vpx });
    var factory = new DesktopCapturerFactory(provider, options);

    var ex = Assert.Throws<NotSupportedException>(() => factory.CreateNew());

    Assert.Contains("No viewer decodes", ex.Message);
  }
}
