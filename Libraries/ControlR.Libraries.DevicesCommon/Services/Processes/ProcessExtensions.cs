using System.Diagnostics;

namespace ControlR.Libraries.DevicesCommon.Services.Processes;

public static class ProcessExtensions
{
  public static void KillAndDispose(this IProcess process)
  {
    try
    {
      process.Kill();
    }
    catch { }
    try
    {
      process.Dispose();
    }
    catch { }
  }
}