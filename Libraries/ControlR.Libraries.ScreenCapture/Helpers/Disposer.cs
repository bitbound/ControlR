namespace ControlR.Libraries.ScreenCapture.Helpers;
public static class Disposer
{
    public static void TryDispose(params IDisposable?[] disposables)
    {
        foreach (var disposable in disposables)
        {
          try
          {
            disposable?.Dispose();
          }
          catch
          {
            // Intentionally ignored.
          }
        }
    }
}
