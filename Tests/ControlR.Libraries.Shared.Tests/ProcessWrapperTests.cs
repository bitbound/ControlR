using System.Diagnostics;
using ControlR.Libraries.Shared.Services.Processes;

namespace ControlR.Libraries.Shared.Tests;

public class ProcessWrapperTests
{
  [Fact]
  public async Task Exited_DoesNotRaiseRemovedSubscriber_WhenSubscriberUnsubscribedBeforeExit()
  {
    using var process = CreateExitProcess();
    using var wrapper = new ProcessWrapper(process);
    var removedHandlerInvocationCount = 0;
    var activeHandlerCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    EventHandler<IProcess> removedHandler = (_, _) =>
    {
      Interlocked.Increment(ref removedHandlerInvocationCount);
    };

    wrapper.Exited += removedHandler;
    wrapper.Exited -= removedHandler;
    wrapper.Exited += (_, _) =>
    {
      activeHandlerCompleted.TrySetResult();
    };

    process.Start();

    await activeHandlerCompleted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await process.WaitForExitAsync(TestContext.Current.CancellationToken);
    await Task.Delay(100, TestContext.Current.CancellationToken);

    Assert.Equal(0, removedHandlerInvocationCount);
  }

  [Fact]
  public async Task Exited_RaisesEachSubscriberOnce_WhenMultipleSubscribersRegistered()
  {
    using var process = CreateExitProcess();
    using var wrapper = new ProcessWrapper(process);
    var firstInvocationCount = 0;
    var secondInvocationCount = 0;
    var allHandlersCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    wrapper.Exited += (_, _) =>
    {
      if (Interlocked.Increment(ref firstInvocationCount) == 1 && Volatile.Read(ref secondInvocationCount) == 1)
      {
        allHandlersCompleted.TrySetResult();
      }
    };

    wrapper.Exited += (_, _) =>
    {
      if (Interlocked.Increment(ref secondInvocationCount) == 1 && Volatile.Read(ref firstInvocationCount) == 1)
      {
        allHandlersCompleted.TrySetResult();
      }
    };

    process.Start();

    await allHandlersCompleted.Task.WaitAsync(TestContext.Current.CancellationToken);

    await process.WaitForExitAsync(TestContext.Current.CancellationToken);

    Assert.Equal(1, firstInvocationCount);
    Assert.Equal(1, secondInvocationCount);
  }

  [Fact]
  public async Task Exited_RaisesRemainingSubscriberOnce_WhenAnotherSubscriberIsRemoved()
  {
    using var process = CreateExitProcess();
    using var wrapper = new ProcessWrapper(process);
    var removedHandlerInvocationCount = 0;
    var remainingHandlerInvocationCount = 0;
    var remainingHandlerCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    EventHandler<IProcess> removedHandler = (_, _) =>
    {
      Interlocked.Increment(ref removedHandlerInvocationCount);
    };

    EventHandler<IProcess> remainingHandler = (_, _) =>
    {
      if (Interlocked.Increment(ref remainingHandlerInvocationCount) == 1)
      {
        remainingHandlerCompleted.TrySetResult();
      }
    };

    wrapper.Exited += removedHandler;
    wrapper.Exited += remainingHandler;
    wrapper.Exited -= removedHandler;

    process.Start();

    await remainingHandlerCompleted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await process.WaitForExitAsync(TestContext.Current.CancellationToken);
    await Task.Delay(100, TestContext.Current.CancellationToken);

    Assert.Equal(0, removedHandlerInvocationCount);
    Assert.Equal(1, remainingHandlerInvocationCount);
  }

  [Fact]
  public async Task Exited_RaisesSubscriberOnce_WhenProcessExits()
  {
    using var process = CreateExitProcess();
    using var wrapper = new ProcessWrapper(process);
    var invocationCount = 0;
    var exitedProcess = new TaskCompletionSource<IProcess>(TaskCreationOptions.RunContinuationsAsynchronously);

    wrapper.Exited += (_, wrappedProcess) =>
    {
      Interlocked.Increment(ref invocationCount);
      exitedProcess.TrySetResult(wrappedProcess);
    };

    process.Start();

    var raisedProcess = await exitedProcess.Task.WaitAsync(TestContext.Current.CancellationToken);

    await process.WaitForExitAsync(TestContext.Current.CancellationToken);

    Assert.Same(wrapper, raisedProcess);
    Assert.Equal(1, invocationCount);
  }

  private static Process CreateExitProcess()
  {
    return new Process
    {
      EnableRaisingEvents = true,
      StartInfo = new ProcessStartInfo
      {
        FileName = "dotnet",
        Arguments = "--version",
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      }
    };
  }
}