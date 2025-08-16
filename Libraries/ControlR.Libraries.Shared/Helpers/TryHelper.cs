namespace ControlR.Libraries.Shared.Helpers;

public static class TryHelper
{
  public static void TryAll(
    params Action[] actions)
  {
    foreach (var action in actions)
    {
      try
      {
        action();
      }
      catch
      {
        // Ignore.
      }
    }
  }

  public static void TryAll(
    Action<Exception> onError,
    params Action[] actions)
  {
    foreach (var action in actions)
    {
      try
      {
        action();
      }
      catch (Exception ex)
      {
        try
        {
          onError(ex);
        }
        catch
        {
          // Ignore.
        }
      }
    }
  }
}