using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Viewer.Common.Options;
using ControlR.Viewer.Avalonia.Services.Navigation;

namespace ControlR.AvaloniaViewerExample.ViewModels;

public interface IMainWindowViewModel : INotifyPropertyChanged
{
  ViewerPage ActivePage { get; set; }
  bool IsDarkMode { get; set; }
  ControlrViewerOptions ViewerOptions { get; }
}

public partial class MainWindowViewModel(ControlrViewerOptions viewerOptions) : ObservableObject, IMainWindowViewModel
{
  [ObservableProperty]
  public partial ViewerPage ActivePage { get; set; } = ViewerPage.RemoteControl;
  
  [ObservableProperty]
  public partial bool IsDarkMode { get; set; } = true;
  public ControlrViewerOptions ViewerOptions { get; } = viewerOptions;
}
