using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ControlR.DesktopClient.Windows.Services;


internal class InputSimulatorWindows : IInputSimulator
{
  private readonly BlockingCollection<Action> _actionQueue = [];
  private readonly IHostApplicationLifetime _appLifetime;
  private readonly ILogger<InputSimulatorWindows> _logger;
  private readonly Thread _processorThread;
  private readonly IWin32Interop _win32Interop;
  public InputSimulatorWindows(
     IWin32Interop win32Interop,
     IHostApplicationLifetime appLifetime,
     ILogger<InputSimulatorWindows> logger)
  {
    _win32Interop = win32Interop;
    _appLifetime = appLifetime;
    _logger = logger;
    _processorThread = new Thread(() =>
    {
      _logger.LogInformation("Input simulator processor thread started.");
      ProcessActions();
    });
    _processorThread.Start();
  }

  public void InvokeKeyEvent(string key, bool isPressed)
  {
    if (string.IsNullOrEmpty(key))
    {
      _logger.LogWarning("Key cannot be empty.");
      return;
    }

    _actionQueue.Add(() =>
    {
      var result = _win32Interop.InvokeKeyEvent(key, isPressed);
      if (!result.IsSuccess)
      {
        _logger.LogWarning("Failed to invoke key event. Key: {Key}, IsPressed: {IsPressed}, Reason: {Reason}", key,
          isPressed, result.Reason);
      }
    });
  }

  public void InvokeMouseButtonEvent(int x, int y, DisplayInfo? display, int button, bool isPressed)
  {
    _actionQueue.Add(() => { _win32Interop.InvokeMouseButtonEvent(x, y, button, isPressed); });
  }

  public void MovePointer(int x, int y, DisplayInfo? display, MovePointerType moveType)
  {
    _actionQueue.Add(() => { _win32Interop.MovePointer(x, y, moveType); });
  }

  public void ResetKeyboardState()
  {
    _actionQueue.Add(() => { _win32Interop.ResetKeyboardState(); });
  }

  public void ScrollWheel(int x, int y, DisplayInfo? display, int scrollY, int scrollX)
  {
    _actionQueue.Add(() => { _win32Interop.InvokeWheelScroll(x, y, scrollY, scrollX); });
  }

  public void TypeText(string text)
  {
    _actionQueue.Add(() => { _win32Interop.TypeText(text); });
  }

  private void ProcessActions()
  {
    var consumerStream = _actionQueue.GetConsumingEnumerable(_appLifetime.ApplicationStopping);

    try
    {
      foreach (var action in consumerStream)
      {
        try
        {
          _win32Interop.SwitchToInputDesktop();
          action.Invoke();
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error processing input simulator action.");
        }
      }
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Stopping input simulator. Application is shutting down.");
    }
  }
}