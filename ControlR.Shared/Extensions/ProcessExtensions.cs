using System.Diagnostics;

namespace ControlR.Shared.Extensions;

public static class ProcessExtensions
{
    public static void KillAndDispose(this Process process)
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