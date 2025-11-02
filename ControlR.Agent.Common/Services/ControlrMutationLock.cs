using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace ControlR.Agent.Common.Services;

/// <summary>
/// Global mutation lock for coordinating any file system or process mutations performed by ControlR.
/// Use this whenever an operation could modify installed binaries, archives, services, or running processes.
/// </summary>
internal interface IControlrMutationLock
{
  /// <summary>
  /// Acquire the global mutation lock, waiting until it becomes available or the token is cancelled.
  /// </summary>
  /// <returns>An IDisposable that must be disposed to release the lock.</returns>
  Task<IDisposable> AcquireAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Try to acquire the global mutation lock within the specified timeout.
  /// </summary>
  /// <returns>An IDisposable when acquired; null if the timeout elapses before acquisition.</returns>
  Task<IDisposable?> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>
/// Cross-platform global mutation lock implementation.
/// Combines in-process async semaphore with a machine-wide mutex on Windows,
/// and a file lock on Linux/macOS, to prevent concurrent mutations.
/// </summary>
internal sealed class ControlrMutationLock(
  ISystemEnvironment systemEnvironment,
  IFileSystem fileSystem,
  IOptions<InstanceOptions> instanceOptions,
  ILogger<ControlrMutationLock> logger) : IControlrMutationLock
{
  private readonly IFileSystem _fileSystem = fileSystem;
  private readonly SemaphoreSlim _inProc = new(1, 1);
  private readonly IOptions<InstanceOptions> _instanceOptions = instanceOptions;
  private readonly ILogger<ControlrMutationLock> _logger = logger;
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;


  public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
  {
    // Acquire in-proc first. If external acquisition fails, release this.
    await _inProc.WaitAsync(cancellationToken);

    IDisposable? external = null;
    try
    {
      if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
      {
        external = AcquireWindowsMutex(cancellationToken);
      }
      else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
      {
        external = AcquireFileLock(cancellationToken);
      }
      else
      {
        throw new PlatformNotSupportedException();
      }

      return new Releaser(_inProc, external);
    }
    catch (Exception ex)
    {
      // If external acquisition failed, release in-proc before bubbling up
      _logger.LogError(ex, "Failed to acquire global mutation lock.");
      _inProc.Release();
      external?.Dispose();
      throw;
    }
  }


  public async Task<IDisposable?> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken)
  {
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    cts.CancelAfter(timeout);
    try
    {
      return await AcquireAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
      return null;
    }
  }

  private FileLockHolder AcquireFileLock(CancellationToken cancellationToken)
  {
    var lockPath = GetLockFilePath();
    var lockDir = Path.GetDirectoryName(lockPath)!;
    _fileSystem.CreateDirectory(lockDir);

    // Try to open a stream with exclusive access. Holding this handle serializes other lockers
    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();
      try
      {
        var stream = _fileSystem.OpenFileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        return new FileLockHolder(stream);
      }
      catch (IOException)
      {
        // Someone else holds the lock; back off and retry
        Thread.Sleep(200);
      }
    }
  }

  [SupportedOSPlatform("windows6.0.6000")]
  private MutexHolder AcquireWindowsMutex(CancellationToken cancellationToken)
  {
    // Use Global\ to coordinate across sessions (services vs. user sessions)
    var name = GetGlobalName();
    using var mutex = new Mutex(false, name, out _);

    // Wait with cancellation support by polling, to avoid WaitHandle.WaitAny complexities
    while (true)
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (mutex.WaitOne(TimeSpan.FromMilliseconds(200)))
      {
        // Hand the mutex ownership to the releaser via a wrapper
        return new MutexHolder(mutex);
      }
    }
  }

  private string GetGlobalName()
  {
    var instanceId = _instanceOptions.Value.InstanceId;
    return string.IsNullOrWhiteSpace(instanceId)
      ? "Global\\ControlR.Mutation"
      : $"Global\\ControlR.Mutation.{instanceId}";
  }

  private string GetLockFilePath()
  {
    var baseDir = _systemEnvironment.StartupDirectory;
    var instanceId = _instanceOptions.Value.InstanceId;
    var lockDir = string.IsNullOrWhiteSpace(instanceId)
      ? Path.Combine(baseDir, "locks")
      : Path.Combine(baseDir, "locks", instanceId);

    return Path.Combine(lockDir, "controlr.mutation.lock");
  }



  private sealed class FileLockHolder(FileStream stream) : IDisposable
  {
    private readonly FileStream _stream = stream;
    private bool _disposed;

    public void Dispose()
    {
      if (_disposed) return;
      _disposed = true;
      // Exclusive handle is sufficient; Unlock is not supported on macOS
      _stream.Dispose();
    }
  }

  private sealed class MutexHolder : IDisposable
  {
    private readonly Mutex _mutex;
    private bool _disposed;

    public MutexHolder(Mutex mutex)
    {
      // We need to keep the same instance; prevent using-statement from disposing it prematurely
      _mutex = mutex;
      GC.KeepAlive(_mutex);
    }

    public void Dispose()
    {
      if (_disposed) return;
      _disposed = true;
      try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
      _mutex.Dispose();
    }
  }

  private sealed class Releaser(SemaphoreSlim sem, IDisposable external) : IDisposable
  {
    private readonly IDisposable _external = external;
    private readonly SemaphoreSlim _sem = sem;
    private bool _disposed;

    public void Dispose()
    {
      if (_disposed) return;
      _disposed = true;
      try { _external.Dispose(); } catch { /* best-effort */ }
      _sem.Release();
    }
  }
}
