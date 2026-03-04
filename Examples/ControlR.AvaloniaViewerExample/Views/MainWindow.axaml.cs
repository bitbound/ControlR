using Avalonia.Controls;

namespace ControlR.AvaloniaViewerExample.Views;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();
  }

  private void IconButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
  {
    ConnectPanel.IsVisible  = false;
    Viewer.IsVisible = true;
  }
}