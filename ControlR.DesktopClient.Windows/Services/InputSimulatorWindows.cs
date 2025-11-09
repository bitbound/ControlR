using System.Collections.Concurrent;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Windows.Services;

internal class InputSimulatorWindows(
  IWin32Interop win32Interop,
  ILogger<InputSimulatorWindows> logger) : IInputSimulator, IHostedService
{
  private readonly BlockingCollection<Action> _actionQueue = [];
  private readonly ILogger<InputSimulatorWindows> _logger = logger;
  private readonly CancellationTokenSource _processorCts = new();
  private readonly IWin32Interop _win32Interop = win32Interop;
  private Thread? _processorThread;

  public void InvokeKeyEvent(string key, string? code, bool isPressed)
  {
    if (string.IsNullOrEmpty(key))
    {
      _logger.LogWarning("Key cannot be empty.");
      return;
    }

    // Hybrid approach: route printable characters to Unicode injection, commands to virtual key simulation
    // When code is null/empty, it indicates a printable character that should be typed (not simulated as key)
    var isPrintableCharacter = string.IsNullOrWhiteSpace(code) && key.Length == 1;

    if (isPrintableCharacter)
    {
      // For printable characters, use Unicode injection on key down only
      // Key up events are ignored since TypeText handles both down and up internally
      if (isPressed)
      {
        _actionQueue.Add(() => _win32Interop.TypeText(key));
      }
    }
    else
    {
      // For commands, shortcuts, and non-printable keys, use virtual key simulation
      _actionQueue.Add(() => _win32Interop.InvokeKeyEvent(key, code, isPressed));
    }
  }

  public void InvokeMouseButtonEvent(int x, int y, DisplayInfo? display, int button, bool isPressed)
  {
    _actionQueue.Add(() => _win32Interop.InvokeMouseButtonEvent(x, y, button, isPressed));
  }

  public void MovePointer(int x, int y, DisplayInfo? display, MovePointerType moveType)
  {
    _actionQueue.Add(() => _win32Interop.MovePointer(x, y, moveType));
  }

  public void ResetKeyboardState()
  {
    _actionQueue.Add(() => _win32Interop.ResetKeyboardState());
  }

  public void ScrollWheel(int x, int y, DisplayInfo? display, int scrollY, int scrollX)
  {
    _actionQueue.Add(() => _win32Interop.InvokeWheelScroll(x, y, scrollY, scrollX));
  }
  
  public Task SetBlockInput(bool isBlocked)
  {
    _actionQueue.Add(() => _win32Interop.SetBlockInput(isBlocked));
    return Task.CompletedTask;
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting input simulator processor thread.");
    // When we implement BlockInput, it requires simulated input to be called from the same thread 
    // that called BlockInput. So we create a dedicated thread for processing input simulation.
    _processorThread = new Thread(ProcessActions);
    _processorThread.Start();
    return Task.CompletedTask;
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Stopping input simulator processor thread.");
    await _processorCts.CancelAsync();
    _processorThread?.Join(TimeSpan.FromSeconds(5));
    _logger.LogInformation("Input simulator processor thread stopped.");
  }

  public void TypeText(string text)
  {
    _actionQueue.Add(() => { _win32Interop.TypeText(text); });
  }

  private void ProcessActions()
  {
    var consumerStream = _actionQueue.GetConsumingEnumerable(_processorCts.Token);

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