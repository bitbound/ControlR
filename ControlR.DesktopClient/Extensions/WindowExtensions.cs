using Avalonia.Controls;

namespace ControlR.DesktopClient.Extensions;

public static class WindowExtensions
{
  public static async Task<TReturnValue?> ShowHeadlessDialog<TWindow, TViewModel, TReturnValue>(
    this TWindow window,
    Func<TViewModel, TReturnValue> getReturnValue,
    WindowStartupLocation windowStartupLocation = WindowStartupLocation.CenterScreen,
    CancellationToken cancellationToken = default) where TWindow : Window
  {
    TReturnValue? returnValue = default;
    var tcs = new TaskCompletionSource();
    
    window.WindowStartupLocation = windowStartupLocation;
    window.Closed += (sender, args) =>
    {
      if (window.DataContext is TViewModel viewModel)
      {
        returnValue = getReturnValue(viewModel);
      }
      tcs.SetResult();
    };
    window.Show();
    window.Activate();

    await tcs.Task.WaitAsync(cancellationToken);
    return returnValue;
  }
}