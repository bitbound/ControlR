using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ControlR.DesktopClient.Windows.Services;

/// <summary>
/// Provides a dedicated STA thread with a Windows message pump for window operations.
/// </summary>
public interface IWindowsMessagePump : IHostedService
{
  /// <summary>
  /// Invokes an action on the Windows message pump thread and waits for the result.
  /// </summary>
  Task<T> InvokeOnWindowThread<T>(Func<T> action);

  /// <summary>
  /// Invokes an action on the Windows message pump thread.
  /// </summary>
  Task InvokeOnWindowThread(Action action);
}


[SupportedOSPlatform("windows6.1")]
internal class WindowsMessagePump(ILogger<WindowsMessagePump> logger)
  : BackgroundService, IWindowsMessagePump
{
  private readonly ILogger<WindowsMessagePump> _logger = logger;
  private readonly BlockingCollection<WorkItem> _workQueue = [];

  private Thread? _messageThread;

  public Task<T> InvokeOnWindowThread<T>(Func<T> action)
  {
    if (_workQueue.IsAddingCompleted)
    {
      return Task.FromException<T>(new InvalidOperationException("The message pump is stopping and cannot accept new work items."));
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

  public Task InvokeOnWindowThread(Action action)
  {
    if (_workQueue.IsAddingCompleted)
    {
      return Task.FromException(new InvalidOperationException("The message pump is stopping and cannot accept new work items."));
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

  public override async Task StopAsync(CancellationToken cancellationToken)
  {
    _workQueue.CompleteAdding();
    if (_messageThread?.IsAlive == true)
    {
      _messageThread.Join(TimeSpan.FromSeconds(5));
    }
    await base.StopAsync(cancellationToken);
  }

  protected override Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _messageThread = new Thread(() => RunMessageLoop(stoppingToken))
    {
      Name = "Windows Message Pump",
      IsBackground = false
    };
    _messageThread.SetApartmentState(ApartmentState.STA);
    _messageThread.Start();
    return Task.CompletedTask;
  }

  private void RunMessageLoop(CancellationToken stoppingToken)
  {
    try
    {
      _logger.LogInformation("Windows message pump thread started");

      while (!stoppingToken.IsCancellationRequested && !_workQueue.IsCompleted)
      {
        // Process any queued work items
        while (_workQueue.TryTake(out var workItem, 10))
        {
          try
          {
            workItem.Action();
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "Error executing work item on message pump thread");
          }
        }

        // Process Windows messages
        unsafe
        {
          MSG msg;
          while (PInvoke.PeekMessage(&msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
          {
            if (msg.message == 0x0012) // WM_QUIT
            {
              _logger.LogInformation("WM_QUIT received, exiting message loop");
              return;
            }

            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
          }
        }

        Thread.Sleep(1);
      }

      _logger.LogInformation("Windows message pump thread exiting normally");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in Windows message pump thread");
    }
  }

  private class WorkItem
  {
    public required Action Action { get; init; }
  }
}
