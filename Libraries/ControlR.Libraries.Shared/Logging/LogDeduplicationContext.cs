namespace ControlR.Libraries.Shared.Logging;

internal sealed class LogDeduplicationContext
{
  private static readonly AsyncLocal<bool> _isEnabled = new();

  public static bool IsEnabled => _isEnabled.Value;

  public static IDisposable EnterScope()
  {
    var previous = _isEnabled.Value;
    _isEnabled.Value = true;
    return new CallbackDisposable(() => _isEnabled.Value = previous);
  }
}