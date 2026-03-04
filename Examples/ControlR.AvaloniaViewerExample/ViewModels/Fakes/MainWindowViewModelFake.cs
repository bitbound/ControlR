
using ControlR.Libraries.Viewer.Common.Options;

namespace ControlR.AvaloniaViewerExample.ViewModels.Fakes;

public class MainWindowViewModelFake : IMainWindowViewModel
{
  public ControlrViewerOptions ViewerOptions { get; } = new()
  {
    BaseUrl = new Uri("https://controlr.example.com"),
    DeviceId = Guid.NewGuid(),
    PersonalAccessToken = "fake-token"
  };
}
