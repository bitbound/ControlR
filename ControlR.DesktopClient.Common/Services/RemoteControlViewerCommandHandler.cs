using Bitbound.SimpleMessenger;
using ControlR.DesktopClient.Common.Messages;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.State;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using ControlR.Libraries.Shared.Constants;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Common.Services;

/// <summary>
/// Executes remote control commands issued by the viewer, independent of the transport that carried them.
/// Results that the viewer needs beyond success or failure are published over the messenger for the
/// session's stream to deliver.
/// </summary>
public interface IRemoteControlViewerCommandHandler
{
  Task<Result> Handle(RemoteControlViewerWrapper wrapper);
}

internal sealed class RemoteControlViewerCommandHandler(
  IHostApplicationLifetime appLifetime,
  IClipboardManager clipboardManager,
  IDesktopCapturerFactory desktopCapturerFactory,
  IDisplayManager displayManager,
  IInputSimulator inputSimulator,
  IMessenger messenger,
  IRemoteControlSessionState sessionState,
  ISystemEnvironment systemEnvironment,
  IOptions<RemoteControlSessionOptions> startupOptions,
  ILogger<RemoteControlViewerCommandHandler> logger) : IRemoteControlViewerCommandHandler
{
  private readonly IHostApplicationLifetime _appLifetime = appLifetime;
  private readonly IClipboardManager _clipboardManager = clipboardManager;
  private readonly IDesktopCapturer _desktopCapturer = desktopCapturerFactory.GetOrCreate();
  private readonly IDisplayManager _displayManager = displayManager;
  private readonly IInputSimulator _inputSimulator = inputSimulator;
  private readonly ILogger<RemoteControlViewerCommandHandler> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly IRemoteControlSessionState _sessionState = sessionState;
  private readonly RemoteControlSessionOptions _startupOptions = startupOptions.Value;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;

  private bool _isInputBlocked;

  public async Task<Result> Handle(RemoteControlViewerWrapper wrapper)
  {
    try
    {
      using var logScope = _logger.BeginMemberScope();

      switch (wrapper.DtoType)
      {
        case RemoteControlViewerDtoType.CloseRemoteControlSession:
          {
            _logger.LogInformation("Received request to close remote control session.");
            _appLifetime.StopApplication();
            break;
          }
        case RemoteControlViewerDtoType.ChangeDisplays:
          {
            var payload = wrapper.GetPayload<ChangeDisplaysDto>();
            await _desktopCapturer.ChangeDisplays(payload.DisplayId);
            break;
          }
        case RemoteControlViewerDtoType.WheelScroll:
          {
            var payload = wrapper.GetPayload<WheelScrollDto>();
            var coordinates = await TryGetPointerCoordinates(payload.NormalizedX, payload.NormalizedY);
            if (coordinates == null)
            {
              break;
            }
            await _inputSimulator.ScrollWheel(coordinates, (int)payload.ScrollY, (int)payload.ScrollX);
            break;
          }
        case RemoteControlViewerDtoType.KeyEvent:
          {
            var payload = wrapper.GetPayload<KeyEventDto>();
            await _inputSimulator.InvokeKeyEvent(
              payload.Key,
              payload.Code,
              payload.IsPressed,
              payload.InputMode,
              payload.Modifiers);
            break;
          }
        case RemoteControlViewerDtoType.ResetKeyboardState:
          {
            await _inputSimulator.ResetKeyboardState();
            break;
          }
        case RemoteControlViewerDtoType.ClipboardText:
          {
            _logger.LogInformation("Received clipboard text from viewer.");
            var payload = wrapper.GetPayload<ClipboardTextDto>();
            await _clipboardManager.SetText(payload.Text);
            break;
          }
        case RemoteControlViewerDtoType.RequestClipboardText:
          {
            _logger.LogInformation("Received request for clipboard text.");
            await SendCurrentClipboardText();
            break;
          }
        case RemoteControlViewerDtoType.TypeText:
          {
            var payload = wrapper.GetPayload<TypeTextDto>();
            await _inputSimulator.TypeText(payload.Text);
            break;
          }
        case RemoteControlViewerDtoType.MovePointer:
          {
            var payload = wrapper.GetPayload<MovePointerDto>();
            var coordinates = await TryGetPointerCoordinates(payload.NormalizedX, payload.NormalizedY);
            if (coordinates == null)
            {
              break;
            }
            await _inputSimulator.MovePointer(coordinates, MovePointerType.Absolute);
            break;
          }
        case RemoteControlViewerDtoType.MouseButtonEvent:
          {
            var payload = wrapper.GetPayload<MouseButtonEventDto>();
            var coordinates = await TryGetPointerCoordinates(payload.NormalizedX, payload.NormalizedY);
            if (coordinates == null)
            {
              break;
            }
            await _inputSimulator.MovePointer(coordinates, MovePointerType.Absolute);
            await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, payload.IsPressed);
            break;
          }
        case RemoteControlViewerDtoType.MouseClick:
          {
            var payload = wrapper.GetPayload<MouseClickDto>();
            var coordinates = await TryGetPointerCoordinates(payload.NormalizedX, payload.NormalizedY);
            if (coordinates == null)
            {
              break;
            }
            await _inputSimulator.MovePointer(coordinates, MovePointerType.Absolute);
            await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, true);
            await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, false);

            if (payload.IsDoubleClick)
            {
              await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, true);
              await _inputSimulator.InvokeMouseButtonEvent(coordinates, payload.Button, false);
            }
            break;
          }
        case RemoteControlViewerDtoType.RequestKeyFrame:
          {
            _logger.LogInformation("Received request for key frame.");
            await _desktopCapturer.RequestKeyFrame();
            break;
          }
        case RemoteControlViewerDtoType.UpdateCaptureSettings:
          {
            var payload = wrapper.GetPayload<UpdateCaptureSettingsDto>();
            ApplyCaptureSettings(payload);
            await _desktopCapturer.RequestKeyFrame();
            break;
          }
        case RemoteControlViewerDtoType.ToggleBlockInput:
          {
            if (!_systemEnvironment.IsWindows())
            {
              _logger.LogWarning("ToggleBlockInput is only supported on Windows. Ignoring request.");
              break;
            }

            var payload = wrapper.GetPayload<ToggleBlockInputDto>();
            _logger.LogInformation("Toggling block input to {isEnabled}.", payload.IsEnabled);
            var success = await _inputSimulator.SetBlockInput(payload.IsEnabled);
            _isInputBlocked = success ? payload.IsEnabled : _isInputBlocked;
            var resultDto = new BlockInputResultDto(success, _isInputBlocked);
            await SendDtoToViewer(RemoteControlDesktopWrapper.Create(resultDto, RemoteControlDesktopDtoType.BlockInputResult));
            break;
          }
        case RemoteControlViewerDtoType.TogglePrivacyScreen:
          {
            if (!_systemEnvironment.IsWindows())
            {
              _logger.LogWarning("TogglePrivacyScreen is only supported on Windows. Ignoring request.");
              break;
            }

            var payload = wrapper.GetPayload<TogglePrivacyScreenDto>();
            _logger.LogInformation("Toggling privacy screen to {isEnabled}.", payload.IsEnabled);
            var result = await _displayManager.SetPrivacyScreen(payload.IsEnabled);
            var resultDto = new PrivacyScreenResultDto(result.IsSuccess, _displayManager.IsPrivacyScreenEnabled);
            await SendDtoToViewer(
              RemoteControlDesktopWrapper.Create(resultDto, RemoteControlDesktopDtoType.PrivacyScreenResult));
            break;
          }
        default:
          _logger.LogWarning("Unhandled DTO type: {type}", wrapper.DtoType);
          return Result.Fail($"Unhandled remote control DTO type: {wrapper.DtoType}");
      }

      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling DTO.");
      return Result.Fail(ex);
    }
  }

  private void ApplyCaptureSettings(UpdateCaptureSettingsDto payload)
  {
    _sessionState.CaptureCursor = payload.CaptureCursor;
    _sessionState.EnableDirectX = payload.EnableDirectX;
    _sessionState.EncodingFormat = payload.EncodingFormat;
    _sessionState.IsAutoQualityEnabled = payload.IsAutoQualityEnabled;
    _sessionState.ImageQuality = payload.ManualQuality;

    var autoQualityMinimum = payload.AutoQualityMinimum >= 1
      ? payload.AutoQualityMinimum
      : AppConstants.DefaultRemoteControlAutoQualityMinimum;
    var autoQualityMaximum = payload.AutoQualityMaximum >= 2
      ? payload.AutoQualityMaximum
      : AppConstants.DefaultRemoteControlAutoQualityMaximum;

    _sessionState.AutoQualityLowerThresholdMbps = payload.AutoQualityLowerThresholdMbps;
    _sessionState.AutoQualityMaximum = autoQualityMaximum;
    _sessionState.AutoQualityMinimum = autoQualityMinimum;
    _sessionState.AutoQualityUpperThresholdMbps = payload.AutoQualityUpperThresholdMbps;
    _sessionState.IsMaxBandwidthEnabled = payload.IsMaxBandwidthEnabled;
    _sessionState.MaxBandwidthMbps = payload.MaxBandwidthMbps;
  }

  private async Task SendCurrentClipboardText()
  {
    var clipboardText = await _clipboardManager.GetText();
    var dto = new ClipboardTextDto(clipboardText, _startupOptions.SessionId);
    await SendDtoToViewer(RemoteControlDesktopWrapper.Create(dto, RemoteControlDesktopDtoType.ClipboardText));
  }

  private Task SendDtoToViewer(RemoteControlDesktopWrapper wrapper)
  {
    _messenger.Send(new SendRemoteControlDtoToViewerMessage(wrapper));
    return Task.CompletedTask;
  }

  private async Task<PointerCoordinates?> TryGetPointerCoordinates(double normalizedX, double normalizedY)
  {
    var selectResult = await _desktopCapturer.TryGetSelectedDisplay();
    if (!selectResult.IsSuccess)
    {
      _logger.LogWarning("Selected display is invalid. Unable to process viewer request.");
      return null;
    }

    var selectedDisplay = selectResult.Value;
    var findResult = await _displayManager.TryFindDisplay(selectedDisplay.DeviceName);
    if (!findResult.IsSuccess)
    {
      _logger.LogWarning("Selected display {DisplayName} could not be resolved.", selectedDisplay.DeviceName);
      return null;
    }

    return new PointerCoordinates(normalizedX, normalizedY, findResult.Value);
  }
}
