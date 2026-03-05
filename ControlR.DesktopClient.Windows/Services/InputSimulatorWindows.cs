using System.Collections.Concurrent;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Dtos.RemoteControlDtos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Windows.Services;

internal class InputSimulatorWindows(
  IWin32Interop win32Interop,
  ILogger<InputSimulatorWindows> logger) : IInputSimulator, IHostedService
{
  private readonly ILogger<InputSimulatorWindows> _logger = logger;
  private readonly CancellationTokenSource _processorCts = new();
  private readonly IWin32Interop _win32Interop = win32Interop;
  private readonly BlockingCollection<WorkItem> _workQueue = [];

  private bool _isInputBlocked;
  private Thread? _processorThread;

  public Task InvokeKeyEvent(
    string key,
    string code,
    bool isPressed,
    KeyboardInputMode inputMode,
    KeyEventModifiersDto modifiers)
  {
    if (string.IsNullOrEmpty(key))
    {
      _logger.LogWarning("Key cannot be empty.");
      return Task.CompletedTask;
    }

    var mode = inputMode;
    var isPrintableCharacter = key.Length == 1;
    var isModifierPressed = modifiers.AreAnyPressed;
    var isModifierKey = key is "Shift" or "Control" or "Alt" or "Meta";

    if (mode == KeyboardInputMode.Virtual)
    {
      if (isPrintableCharacter && !isModifierPressed && !isModifierKey)
      {
        if (isPressed)
        {
          return InvokeOnInputThread(() => _win32Interop.TypeText(key));
        }

        return Task.CompletedTask;
      }

      if (isModifierPressed && !isModifierKey)
      {
        return InvokeOnInputThread(() => _win32Interop.InvokeKeyEvent(key, code, isPressed, mode));
      }

      return InvokeOnInputThread(() => _win32Interop.InvokeKeyEvent(key, string.Empty, isPressed, mode));
    }

    if (mode == KeyboardInputMode.Auto && isPrintableCharacter && !isModifierPressed)
    {
      if (isPressed)
      {
        return InvokeOnInputThread(() => _win32Interop.TypeText(key));
      }

      return Task.CompletedTask;
    }

    return InvokeOnInputThread(() => _win32Interop.InvokeKeyEvent(key, code, isPressed, mode));
  }

  public Task InvokeMouseButtonEvent(PointerCoordinates coordinates, int button, bool isPressed)
  {
    return InvokeOnInputThread(() =>
      _win32Interop.InvokeMouseButtonEvent(coordinates.PhysicalPoint.X, coordinates.PhysicalPoint.Y, button, isPressed));
  }

  public Task MovePointer(PointerCoordinates coordinates, MovePointerType moveType)
  {
    return InvokeOnInputThread(() =>
      _win32Interop.MovePointer(coordinates.PhysicalPoint.X, coordinates.PhysicalPoint.Y, moveType));
  }

  public Task ResetKeyboardState()
  {
    return InvokeOnInputThread(() => _win32Interop.ResetKeyboardState());
  }

  public Task ScrollWheel(PointerCoordinates coordinates, int scrollY, int scrollX)
  {
    return InvokeOnInputThread(() =>
      _win32Interop.InvokeWheelScroll(coordinates.PhysicalPoint.X, coordinates.PhysicalPoint.Y, scrollY, scrollX));
  }

  public Task<bool> SetBlockInput(bool isBlocked)
  {
    return InvokeOnInputThread(() =>
    {
      var result = _win32Interop.SetBlockInput(isBlocked);
      if (result)
      {
        _isInputBlocked = isBlocked;
      }
      return result;
    });
  }

  public Task StartAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting input simulator processor thread.");
    _processorThread = new Thread(ProcessActions)
    {
      Name = "Input Simulator",
      IsBackground = true
    };
    _processorThread.Start();
    return Task.CompletedTask;
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Stopping input simulator processor thread.");
    _workQueue.CompleteAdding();
    await _processorCts.CancelAsync();
    _processorThread?.Join(TimeSpan.FromSeconds(5));
    _logger.LogInformation("Input simulator processor thread stopped.");
  }

  public Task TypeText(string text)
  {
    return InvokeOnInputThread(() => _win32Interop.TypeText(text));
  }

  private Task<T> InvokeOnInputThread<T>(Func<T> action)
  {
    if (_workQueue.IsAddingCompleted)
    {
      return Task.FromException<T>(new InvalidOperationException("The input simulator is stopping and cannot accept new work items."));
    }

    var tcs = new TaskCompletionSource<T>();
    _workQueue.Add(new WorkItem
    {
      Action = () =>
      {
        try
        {
          var result = action();
          tcs.SetResult(result);
        }
        catch (Exception ex)
        {
          tcs.SetException(ex);
        }
      }
    });
    return tcs.Task;
  }

  private Task InvokeOnInputThread(Action action)
  {
    if (_workQueue.IsAddingCompleted)
    {
      return Task.FromException(new InvalidOperationException("The input simulator is stopping and cannot accept new work items."));
    }

    var tcs = new TaskCompletionSource();
    _workQueue.Add(new WorkItem
    {
      Action = () =>
      {
        try
        {
          action();
          tcs.SetResult();
        }
        catch (Exception ex)
        {
          tcs.SetException(ex);
        }
      }
    });
    return tcs.Task;
  }

  private void ProcessActions()
  {
    var consumerStream = _workQueue.GetConsumingEnumerable(_processorCts.Token);
    try
    {
      foreach (var workItem in consumerStream)
      {
        try
        {
          _win32Interop.SwitchToInputDesktop();
          workItem.Action();
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
    finally
    {
      if (_isInputBlocked)
      {
        _ = _win32Interop.SetBlockInput(false);
      }
    }
  }

  private class WorkItem
  {
    public required Action Action { get; init; }
  }
}