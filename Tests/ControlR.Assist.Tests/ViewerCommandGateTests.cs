using ControlR.Assist.Services;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Assist.Tests;

public class ViewerCommandGateTests
{
  [Theory]
  [InlineData(RemoteControlViewerDtoType.ToggleBlockInput)]
  [InlineData(RemoteControlViewerDtoType.TogglePrivacyScreen)]
  public async Task Handle_NeverAllowedTypes_AreDroppedEvenWhenNotViewOnly(RemoteControlViewerDtoType dtoType)
  {
    var inner = new RecordingHandler();
    var gate = new AssistInputGate();

    var result = await CreateGate(inner, gate).Handle(CreateWrapper(dtoType));

    Assert.True(result.IsSuccess);
    Assert.Empty(inner.Handled);
  }

  [Theory]
  [InlineData(RemoteControlViewerDtoType.KeyEvent)]
  [InlineData(RemoteControlViewerDtoType.RequestClipboardText)]
  [InlineData(RemoteControlViewerDtoType.ClipboardText)]
  public async Task Handle_NotViewOnly_PassesCommandsThrough(RemoteControlViewerDtoType dtoType)
  {
    var inner = new RecordingHandler();
    var gate = new AssistInputGate();

    var result = await CreateGate(inner, gate).Handle(CreateWrapper(dtoType));

    Assert.True(result.IsSuccess);
    Assert.Single(inner.Handled);
  }

  [Theory]
  [InlineData(RemoteControlViewerDtoType.KeyEvent)]
  [InlineData(RemoteControlViewerDtoType.MouseClick)]
  [InlineData(RemoteControlViewerDtoType.MouseButtonEvent)]
  [InlineData(RemoteControlViewerDtoType.MovePointer)]
  [InlineData(RemoteControlViewerDtoType.TypeText)]
  [InlineData(RemoteControlViewerDtoType.WheelScroll)]
  [InlineData(RemoteControlViewerDtoType.RequestClipboardText)]
  [InlineData(RemoteControlViewerDtoType.ClipboardText)]
  public async Task Handle_ViewOnly_DropsInputAndClipboardCommands(RemoteControlViewerDtoType dtoType)
  {
    var inner = new RecordingHandler();
    var gate = new AssistInputGate { IsViewOnly = true };

    var result = await CreateGate(inner, gate).Handle(CreateWrapper(dtoType));

    Assert.True(result.IsSuccess);
    Assert.Empty(inner.Handled);
  }

  private static ViewerCommandGate CreateGate(
    IRemoteControlViewerCommandHandler inner,
    AssistInputGate gate) =>
    new(inner, gate, NullLogger<ViewerCommandGate>.Instance);

  private static RemoteControlViewerWrapper CreateWrapper(RemoteControlViewerDtoType dtoType) =>
    new()
    {
      DtoType = dtoType,
      Payload = [],
      SendTimestamp = TimeProvider.System.GetTimestamp(),
    };

  private sealed class RecordingHandler : IRemoteControlViewerCommandHandler
  {
    public List<RemoteControlViewerDtoType> Handled { get; } = [];

    public Task<Result> Handle(RemoteControlViewerWrapper wrapper)
    {
      Handled.Add(wrapper.DtoType);
      return Task.FromResult(Result.Ok());
    }
  }
}
