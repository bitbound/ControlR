using Avalonia.Controls;
using ControlR.Assist.ViewModels;

namespace ControlR.Assist.Views;

public partial class MainWindow : Window
{
  // The parameterless overload exists so the XAML loader can reach this window by avares URI.
  public MainWindow()
  {
    InitializeComponent();
  }

  public MainWindow(MainWindowViewModel viewModel)
  {
    DataContext = viewModel;
    InitializeComponent();
  }
}
