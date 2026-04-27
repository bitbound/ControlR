using CommunityToolkit.Mvvm.ComponentModel;
using ControlR.Libraries.Viewer.Common.Options;
using ControlR.Viewer.Avalonia.Services.Navigation;

namespace ControlR.AvaloniaViewerExample.ViewModels.Fakes;

public partial class MainWindowViewModelFake : ObservableObject, IMainWindowViewModel
{
  [ObservableProperty]
  private ViewerPage _activePage = ViewerPage.RemoteControl;

  [ObservableProperty]
  private bool _isDarkMode = true;

  public ControlrViewerOptions ViewerOptions { get; } = new()
  {
    BaseUrl = new Uri("https://controlr.example.com"),
    DeviceId = Guid.NewGuid(),
    PersonalAccessToken = "fake-token"
  };
}
