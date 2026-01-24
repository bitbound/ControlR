using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient.Views;

public partial class ToastWindow : Window
{
  private TimeSpan _closeAfter;
  private DispatcherTimer? _timer;
  
  public ToastWindow()
  {
    InitializeComponent();
  }

  public static async Task<ToastWindow> Show(string title, string message, ToastIcon icon, Func<Task>? onClick = null, TimeSpan? closeAfter = null)
  {
    return await Dispatcher.UIThread.InvokeAsync(() =>
    {
      var toastWindow = StaticServiceProvider.Instance.GetRequiredService<ToastWindow>();
      var viewModel = StaticServiceProvider.Instance.GetRequiredService<IToastWindowViewModel>();
      viewModel.Title = title;
      viewModel.Message = message;
      viewModel.ToastIcon = icon;
      viewModel.OnClick = onClick;
      toastWindow.DataContext = viewModel;
      toastWindow._closeAfter = closeAfter ?? TimeSpan.FromSeconds(10);
      toastWindow.Show();

      return toastWindow;
    });
  }

  protected override void OnClosed(EventArgs e)
  {
    _timer?.Stop();
    _timer = null;
    base.OnClosed(e);
  }

  protected override void OnOpened(EventArgs e)
  {
    base.OnOpened(e);
    PositionWindow();
    StartAutoCloseTimer();
  }

  protected override void OnPointerPressed(PointerPressedEventArgs e)
  {
    if (DataContext is IToastWindowViewModel viewModel &&
        viewModel.OnClick is not null)
    {
      viewModel.OnClick.Invoke().Forget();
      Close();
    }
    base.OnPointerPressed(e);
  }

  private void CloseButton_Click(object? sender, RoutedEventArgs e)
  {
    Close();
  }

  private void PositionWindow()
  {
    try
    {
      // Get the primary screen's working area
      var screen = Screens.Primary;
      if (screen?.WorkingArea != null)
      {
        var workingArea = screen.WorkingArea;

        var x = workingArea.Right - (ClientSize.Width * screen.Scaling);
        var y = workingArea.Bottom - (ClientSize.Height * screen.Scaling);

        Position = new Avalonia.PixelPoint((int)x, (int)y);
      }
    }
    catch
    {
      // If positioning fails, just keep the default position
    }
  }

  private void StartAutoCloseTimer()
  {
    _timer = new DispatcherTimer
    {
      Interval = _closeAfter,
    };
    _timer.Tick += (_, _) => Close();
    _timer.Start();
  }
}
