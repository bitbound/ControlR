namespace ControlR.Libraries.WebSocketRelay.Common.Helpers;

internal static class DisposeHelper
{
    public static void DisposeAll(params IDisposable?[] disposables)
    {
        foreach (var disposable in disposables)
        {
            try
            {
                disposable?.Dispose();
            }
            catch { }
        }
    }
}
