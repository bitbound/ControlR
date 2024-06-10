using System.Diagnostics;

namespace ControlR.Libraries.Shared.Extensions;

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