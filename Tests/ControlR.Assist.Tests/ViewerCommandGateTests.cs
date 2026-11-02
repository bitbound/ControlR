using ControlR.Assist.Services;
using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Assist.Tests;

public class ViewerCommandGateTests
{

  // Every viewer command must land in exactly one of the three groups. A type that belongs to none
  // of them is passed through by default, which is how a keyboard-state reset reached a view-only
  // customer, so this fails until a new tag is deliberately classified.
  [Fact]
  public async Task EveryTagIsDeliberatelyClassified()
  {
    var neverAllowed = new[]
    {
      RemoteControlDtoType.ToggleBlockInput,
      RemoteControlDtoType.TogglePrivacyScreen
    };

    var droppedWhileViewOnly = new[]
    {
      RemoteControlDtoType.KeyEvent,
      RemoteControlDtoType.MouseClick,
      RemoteControlDtoType.MouseButtonEvent,
      RemoteControlDtoType.MovePointer,
      RemoteControlDtoType.ResetKeyboardState,
      RemoteControlDtoType.TypeText,
      RemoteControlDtoType.WheelScroll,
      RemoteControlDtoType.RequestClipboardText,
      RemoteControlDtoType.ClipboardText,
      RemoteControlDtoType.ToggleBlockInput,
      RemoteControlDtoType.TogglePrivacyScreen
    };

    var alwaysPassed = new[]
    {
      RemoteControlDtoType.None,
      RemoteControlDtoType.ChangeDisplays,
      RemoteControlDtoType.CloseRemoteControlSession,
      RemoteControlDtoType.RequestKeyFrame,
      RemoteControlDtoType.UpdateCaptureSettings
    };

    // Tags the gate never sees. They are consumed by the stream or only travel desktop to viewer.
    var nonCommandTags = new[]
    {
      RemoteControlDtoType.Ack,
      RemoteControlDtoType.BlockInputResult,
      RemoteControlDtoType.CaptureMetricsChanged,
      RemoteControlDtoType.CursorChanged,
      RemoteControlDtoType.DisplayData,
      RemoteControlDtoType.PrivacyScreenResult,
      RemoteControlDtoType.RemoteControlSessionError,
      RemoteControlDtoType.ScreenRegion,
      RemoteControlDtoType.ScreenRegions,
      RemoteControlDtoType.SessionDisconnectRequested,
      RemoteControlDtoType.ToastNotification,
      RemoteControlDtoType.VideoStreamPacket,
      RemoteControlDtoType.WindowsSessionEnding,
      RemoteControlDtoType.WindowsSessionSwitched
    };

    var allTypes = Enum.GetValues<RemoteControlDtoType>();
    var viewerCommands = neverAllowed.Concat(droppedWhileViewOnly).Concat(alwaysPassed).Distinct().ToArray();
    var classified = viewerCommands.Concat(nonCommandTags);
    Assert.Equal(allTypes.OrderBy(x => x), classified.Distinct().OrderBy(x => x));

    foreach (var dtoType in viewerCommands)
    {
      var viewOnlyInner = new RecordingHandler();
      await CreateGate(viewOnlyInner, new AssistInputGate { IsViewOnly = true }).Handle(CreateWrapper(dtoType));
      Assert.Equal(!droppedWhileViewOnly.Contains(dtoType), viewOnlyInner.Handled.Count == 1);

      var fullControlInner = new RecordingHandler();
      await CreateGate(fullControlInner, new AssistInputGate()).Handle(CreateWrapper(dtoType));
      Assert.Equal(!neverAllowed.Contains(dtoType), fullControlInner.Handled.Count == 1);
    }
  }

  [Theory]
  [InlineData(RemoteControlDtoType.ToggleBlockInput)]
  [InlineData(RemoteControlDtoType.TogglePrivacyScreen)]
  public async Task Handle_NeverAllowedTypes_AreDroppedEvenWhenNotViewOnly(RemoteControlDtoType dtoType)
  {
    var inner = new RecordingHandler();
    var gate = new AssistInputGate();

    var result = await CreateGate(inner, gate).Handle(CreateWrapper(dtoType));

    Assert.True(result.IsSuccess);
    Assert.Empty(inner.Handled);
  }

  [Theory]
  [InlineData(RemoteControlDtoType.KeyEvent)]
  [InlineData(RemoteControlDtoType.RequestClipboardText)]
  [InlineData(RemoteControlDtoType.ClipboardText)]
  public async Task Handle_NotViewOnly_PassesCommandsThrough(RemoteControlDtoType dtoType)
  {
    var inner = new RecordingHandler();
    var gate = new AssistInputGate();

    var result = await CreateGate(inner, gate).Handle(CreateWrapper(dtoType));

    Assert.True(result.IsSuccess);
    Assert.Single(inner.Handled);
  }

  [Theory]
  [InlineData(RemoteControlDtoType.KeyEvent)]
  [InlineData(RemoteControlDtoType.MouseClick)]
  [InlineData(RemoteControlDtoType.MouseButtonEvent)]
  [InlineData(RemoteControlDtoType.MovePointer)]
  [InlineData(RemoteControlDtoType.ResetKeyboardState)]
  [InlineData(RemoteControlDtoType.TypeText)]
  [InlineData(RemoteControlDtoType.WheelScroll)]
  [InlineData(RemoteControlDtoType.RequestClipboardText)]
  [InlineData(RemoteControlDtoType.ClipboardText)]
  public async Task Handle_ViewOnly_DropsInputAndClipboardCommands(RemoteControlDtoType dtoType)
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

  private static RemoteControlDtoWrapper CreateWrapper(RemoteControlDtoType dtoType) =>
    new()
    {
      DtoType = dtoType,
      Payload = [],
      SendTimestamp = TimeProvider.System.GetTimestamp(),
    };

  private sealed class RecordingHandler : IRemoteControlViewerCommandHandler
  {
    public List<RemoteControlDtoType> Handled { get; } = [];

    public Task<Result> Handle(RemoteControlDtoWrapper wrapper)
    {
      Handled.Add(wrapper.DtoType);
      return Task.FromResult(Result.Ok());
    }
  }
}
