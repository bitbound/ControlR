using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Common.State;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ControlR.DesktopClient.Tests;

public class RemoteControlViewerCommandHandlerTests
{
  [Fact]
  public async Task Handle_ClipboardTextRejectedByOperatingSystem_ReturnsThatFailure()
  {
    const string reason = "The clipboard is in use by another application.";
    var clipboardManager = new Mock<IClipboardManager>();
    clipboardManager
      .Setup(x => x.SetText("secret"))
      .ReturnsAsync(Result.Fail(reason));

    var result = await CreateSut(clipboardManager.Object)
      .Handle(CreateClipboardWrapper("secret"));

    Assert.False(result.IsSuccess);
    Assert.Equal(reason, result.Reason);
  }

  [Fact]
  public async Task Handle_ClipboardTextWritten_ReturnsSuccess()
  {
    var clipboardManager = new Mock<IClipboardManager>();
    clipboardManager
      .Setup(x => x.SetText("secret"))
      .ReturnsAsync(Result.Ok());

    var result = await CreateSut(clipboardManager.Object)
      .Handle(CreateClipboardWrapper("secret"));

    Assert.True(result.IsSuccess);
  }

  private static RemoteControlViewerWrapper CreateClipboardWrapper(string text) =>
    RemoteControlViewerWrapper.Create(new ClipboardTextDto(text, Guid.NewGuid()), RemoteControlViewerDtoType.ClipboardText);

  private static RemoteControlViewerCommandHandler CreateSut(IClipboardManager clipboardManager) =>
    new(
      Mock.Of<IHostApplicationLifetime>(),
      clipboardManager,
      new Mock<IDesktopCapturerFactory>().Object,
      Mock.Of<IDisplayManager>(),
      Mock.Of<IInputSimulator>(),
      Mock.Of<IMessenger>(),
      Mock.Of<IRemoteControlSessionState>(),
      Mock.Of<ISystemEnvironment>(),
      Options.Create(new RemoteControlSessionOptions()),
      NullLogger<RemoteControlViewerCommandHandler>.Instance);
}
