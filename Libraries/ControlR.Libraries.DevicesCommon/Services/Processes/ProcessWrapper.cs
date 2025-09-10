using System.Diagnostics;
using System.Runtime.Versioning;

namespace ControlR.Libraries.DevicesCommon.Services.Processes;


/// <summary>
/// <para>
///   An interface that wraps the System.Diagnostics.Process class.
///   Use <see cref="ProcessWrapper"/> for a concrete implementation.
/// </para>
/// <para>
///   A MockProcess class exists in the TestingUtilities project for testing purposes.
/// </para>
/// <para>
///   Add members as needed.
/// </para>
/// </summary>
public interface IProcess : IDisposable
{
  event EventHandler? Exited;
  int BasePriority { get; }
  bool EnableRaisingEvents { get; set; }
  int ExitCode { get; }
  DateTime ExitTime { get; }
  nint Handle { get; }
  int HandleCount { get; }
  bool HasExited { get; }
  int Id { get; }
  string MachineName { get; }
  string ProcessName { get; }
  [SupportedOSPlatform("windows")]
  [SupportedOSPlatform("linux")]
  nint ProcessorAffinity { get; set; }

  bool Responding { get; }
  int SessionId { get; }
  ProcessStartInfo StartInfo { get; set; }

  void Kill();
  void Kill(bool entireProcessTree);
  Task WaitForExitAsync(CancellationToken cancellationToken);
}

public class ProcessWrapper(Process process) : IProcess
{
  private readonly Process _process = process;
  private bool _disposedValue;

  public event EventHandler? Exited
  {
    add
    {
      _process.Exited += value;
    }
    remove
    {
      _process.Exited -= value;
    }
  }

  public int BasePriority => _process.BasePriority;
  public bool EnableRaisingEvents { get => _process.EnableRaisingEvents; set => _process.EnableRaisingEvents = value; }
  public int ExitCode => _process.ExitCode;
  public DateTime ExitTime => _process.ExitTime;
  public nint Handle => _process.Handle;
  public int HandleCount => _process.HandleCount;
  public bool HasExited => _process.HasExited;
  public int Id => _process.Id;
  public string MachineName => _process.MachineName;
  public string ProcessName => _process.ProcessName;
  [SupportedOSPlatform("windows")]
  [SupportedOSPlatform("linux")]
  public nint ProcessorAffinity { get => _process.ProcessorAffinity; set => _process.ProcessorAffinity = value; }

  public bool Responding => _process.Responding;
  public int SessionId => _process.SessionId;
  public ProcessStartInfo StartInfo { get => _process.StartInfo; set => _process.StartInfo = value; }
  public void Kill()
  {
    _process.Kill();
  }

  public void Kill(bool entireProcessTree)
  {
    _process.Kill(entireProcessTree);
  }

  public Task WaitForExitAsync(CancellationToken cancellationToken)
  {
    return _process.WaitForExitAsync(cancellationToken);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_disposedValue)
    {
      if (disposing)
      {
        _process.Dispose();
      }
      _disposedValue = true;
    }
  }

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }
}
