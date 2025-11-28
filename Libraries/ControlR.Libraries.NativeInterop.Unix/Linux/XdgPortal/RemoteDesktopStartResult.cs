namespace ControlR.Libraries.NativeInterop.Unix.Linux.XdgPortal;

public class RemoteDesktopStartResult
{
  public string? RestoreToken { get; init; }
  public required List<PipeWireStreamInfo> Streams { get; init; }
}
