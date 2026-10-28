using ControlR.DesktopClient.Common.Services;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Primitives;
using Microsoft.Extensions.Logging;

namespace ControlR.Assist.Services;

/// <summary>
/// Customer-controlled session permissions, read by the command gate on every inbound viewer command.
/// </summary>
public sealed class AssistInputGate
{
  public volatile bool IsViewOnly;
}

/// <summary>
/// Drops viewer input commands while the customer turns on view-only. Block-input and privacy-screen
/// requests are always dropped so a technician cannot lock the customer out of their own machine.
/// </summary>
public sealed class ViewerCommandGate(
  IRemoteControlViewerCommandHandler inner,
  AssistInputGate gate,
  ILogger<ViewerCommandGate> logger) : IRemoteControlViewerCommandHandler
{
  private static readonly HashSet<RemoteControlViewerDtoType> _inputDtoTypes =
  [
    RemoteControlViewerDtoType.KeyEvent,
    RemoteControlViewerDtoType.MouseClick,
    RemoteControlViewerDtoType.MouseButtonEvent,
    RemoteControlViewerDtoType.MovePointer,
    RemoteControlViewerDtoType.TypeText,
    RemoteControlViewerDtoType.WheelScroll
  ];

  private static readonly HashSet<RemoteControlViewerDtoType> _neverAllowedDtoTypes =
  [
    RemoteControlViewerDtoType.ToggleBlockInput,
    RemoteControlViewerDtoType.TogglePrivacyScreen
  ];

  public async Task<Result> Handle(RemoteControlViewerWrapper wrapper)
  {
    if (_neverAllowedDtoTypes.Contains(wrapper.DtoType))
    {
      logger.LogInformation("Ignored viewer request of type {DtoType}.", wrapper.DtoType);
      return Result.Ok();
    }

    if (gate.IsViewOnly && _inputDtoTypes.Contains(wrapper.DtoType))
    {
      return Result.Ok();
    }

    return await inner.Handle(wrapper);
  }
}
