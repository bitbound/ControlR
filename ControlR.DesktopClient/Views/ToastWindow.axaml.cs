using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.ViewModels;

namespace ControlR.DesktopClient.Views;

public partial class ToastWindow : Window
{
  private DispatcherTimer? _timer;
  public ToastWindow()
  {
    InitializeComponent();
  }

  public static async Task<ToastWindow> Show(string title, string message, ToastIcon icon)
  {
    return await Dispatcher.UIThread.InvokeAsync(() =>
    {
      var toastWindow = new ToastWindow();
      var viewModel = new ToastWindowViewModel
      {
        Title = title,
        Message = message,
        ToastIcon = icon
      };

      toastWindow.DataContext = viewModel;
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

        var x = workingArea.Right - (Width * screen.Scaling);
        var y = workingArea.Bottom - (Height * screen.Scaling);

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
      Interval = TimeSpan.FromSeconds(10),
    };
    _timer.Tick += (_, _) => Close();
    _timer.Start();
  }
}
