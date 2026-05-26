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
  private bool _isInteractiveBearerAuth = true;
  [ObservableProperty]
  private bool _isViewerVisible;
  [ObservableProperty]
  private string _newPassword = string.Empty;
  [ObservableProperty]
  private string _password = string.Empty;
  [ObservableProperty]
  private string _personalAccessToken = "pat-example-token";
  [ObservableProperty]
  private bool _requiresPasswordChange;
  [ObservableProperty]
  private bool _requiresTwoFactor;
  [ObservableProperty]
  private string _serverUrl = "https://controlr.example.com";
  [ObservableProperty]
  private string _statusMessage = "Use your account email and password to connect.";
  [ObservableProperty]
  private string _twoFactorCode = string.Empty;

  public bool IsLoginVisible => !IsViewerVisible;
  public string LoginDescription => ShowPersonalAccessTokenInput
    ? "Enter the ControlR server, device ID, and a personal access token generated from the web app."
    : "Enter the ControlR server, device ID, and the same email and password you use in the web app. The OTP field only appears when the account requires two-factor authentication.";
  public string LoginTitle => ShowPersonalAccessTokenInput
    ? "Connect with a personal access token"
    : "Sign in to the viewer";
  public bool ShowPersonalAccessTokenInput => !IsInteractiveBearerAuth;
  public bool ShowUserPasswordInputs => IsInteractiveBearerAuth;
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
