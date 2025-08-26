namespace ControlR.Libraries.Shared.Helpers;

public static class Disposer
{
  public static void DisposeAll(params IDisposable?[] disposables)
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
  
  public static void DisposeAll(IEnumerable<IDisposable?> disposables)
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
