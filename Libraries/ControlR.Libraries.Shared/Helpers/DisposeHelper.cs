namespace ControlR.Libraries.Shared.Helpers;
public static class DisposeHelper
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
