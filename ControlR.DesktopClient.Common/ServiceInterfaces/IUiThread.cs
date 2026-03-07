namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IUiThread
{
  /// <summary>
  /// Executes the specified action on the UI thread.
  /// If already on the UI thread, the action is executed immediately.
  /// </summary>
  void Invoke(Action action);

  /// <summary>
  /// Executes the specified function on the UI thread and returns its result.
  /// If already on the UI thread, the function is executed immediately.
  /// </summary>
  T Invoke<T>(Func<T> func);

  /// <summary>
  /// Asynchronously executes the specified action on the UI thread.
  /// </summary>
  Task InvokeAsync(Func<Task> func);

  /// <summary>
  /// Asynchronously executes the specified function on the UI thread and returns its result.
  /// If already on the UI thread, the function is executed immediately.
  /// </summary>
  Task<T> InvokeAsync<T>(Func<Task<T>> func);

  /// <summary>
  /// Posts the specified action to be executed on the UI thread without waiting for it to complete.
  /// </summary>
  /// <param name="action">The action to be executed on the UI thread.</param>
  void Post(Action action);
}