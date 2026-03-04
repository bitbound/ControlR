using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Viewer.Common.Options;

namespace ControlR.AvaloniaViewerExample.ViewModels;

public interface IMainWindowViewModel
{
  ControlrViewerOptions ViewerOptions { get; }
}

public partial class MainWindowViewModel(ControlrViewerOptions viewerOptions) : ObservableObject, IMainWindowViewModel
{
  public ControlrViewerOptions ViewerOptions { get; } = viewerOptions;
}
