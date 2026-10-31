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
  // customer, so this fails until a new enum member is deliberately classified.
  [Fact]
  public async Task EveryViewerDtoTypeIsDeliberatelyClassified()
  {
    var neverAllowed = new[]
    {
      RemoteControlViewerDtoType.ToggleBlockInput,
      RemoteControlViewerDtoType.TogglePrivacyScreen
    };

    var droppedWhileViewOnly = new[]
    {
      RemoteControlViewerDtoType.KeyEvent,
      RemoteControlViewerDtoType.MouseClick,
      RemoteControlViewerDtoType.MouseButtonEvent,
      RemoteControlViewerDtoType.MovePointer,
      RemoteControlViewerDtoType.ResetKeyboardState,
      RemoteControlViewerDtoType.TypeText,
      RemoteControlViewerDtoType.WheelScroll,
      RemoteControlViewerDtoType.RequestClipboardText,
      RemoteControlViewerDtoType.ClipboardText,
      RemoteControlViewerDtoType.ToggleBlockInput,
      RemoteControlViewerDtoType.TogglePrivacyScreen
    };

    var alwaysPassed = new[]
    {
      RemoteControlViewerDtoType.None,
      RemoteControlViewerDtoType.ChangeDisplays,
      RemoteControlViewerDtoType.CloseRemoteControlSession,
      RemoteControlViewerDtoType.RequestKeyFrame,
      RemoteControlViewerDtoType.UpdateCaptureSettings
    };

    var allTypes = Enum.GetValues<RemoteControlViewerDtoType>();
    var classified = neverAllowed.Concat(droppedWhileViewOnly).Concat(alwaysPassed);
    Assert.Equal(allTypes.OrderBy(x => x), classified.Distinct().OrderBy(x => x));

    foreach (var dtoType in allTypes)
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
  [InlineData(RemoteControlViewerDtoType.ResetKeyboardState)]
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
