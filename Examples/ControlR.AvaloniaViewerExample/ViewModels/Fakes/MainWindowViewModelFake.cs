using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlR.Libraries.Viewer.Common.Options;
using ControlR.Viewer.Avalonia.Services.Navigation;

namespace ControlR.AvaloniaViewerExample.ViewModels.Fakes;

public partial class MainWindowViewModelFake : ObservableObject, IMainWindowViewModel
{
  [ObservableProperty]
  private ViewerPage _activePage = ViewerPage.RemoteControl;
  [ObservableProperty]
  private string _deviceIdText = Guid.NewGuid().ToString();
  [ObservableProperty]
  private string _email = "viewer@example.com";
  [ObservableProperty]
  private bool _isBusy;
  [ObservableProperty]
  private bool _isDarkMode = true;
  [ObservableProperty]
  private bool _isViewerVisible;
  [ObservableProperty]
  private string _password = string.Empty;
  [ObservableProperty]
  private bool _requiresTwoFactor;
  [ObservableProperty]
  private string _serverUrl = "https://controlr.example.com";
  [ObservableProperty]
  private string _statusMessage = "Use your account email and password to connect.";
  [ObservableProperty]
  private string _twoFactorCode = string.Empty;

  public bool IsLoginVisible => !IsViewerVisible;
  public IAsyncRelayCommand SignInCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
  public IRelayCommand SignOutCommand { get; } = new RelayCommand(() => { });
  public ControlrViewerOptions ViewerOptions { get; } = new()
  {
    AuthenticationMethod = ViewerAuthenticationMethod.InteractiveBearer,
    BaseUrl = new Uri("https://controlr.example.com"),
    DeviceId = Guid.NewGuid()
  };

  public void AttachViewer(Guid viewerInstanceId)
  {
  }

  public void Dispose()
  {
  }
}
