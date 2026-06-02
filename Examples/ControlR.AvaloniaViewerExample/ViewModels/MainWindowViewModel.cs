using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using ControlR.ApiClient;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Viewer.Common.Options;
using ControlR.Viewer.Avalonia.Services;
using ControlR.Viewer.Avalonia.Services.Navigation;
using Microsoft.Extensions.Logging;

namespace ControlR.AvaloniaViewerExample.ViewModels;

public interface IMainWindowViewModel : INotifyPropertyChanged, IDisposable
{
  ViewerPage ActivePage { get; set; }
  string DeviceIdText { get; set; }
  string Email { get; set; }
  bool IsBusy { get; }
  bool IsDarkMode { get; set; }
  bool IsInteractiveBearerAuth { get; set; }
  bool IsLoginVisible { get; }
  bool IsViewerVisible { get; }
  string LoginDescription { get; }
  string LoginTitle { get; }
  string NewPassword { get; set; }
  string Password { get; set; }
  string PersonalAccessToken { get; set; }
  bool RequiresPasswordChange { get; }
  bool RequiresTwoFactor { get; }
  string ServerUrl { get; set; }
  bool ShowPersonalAccessTokenInput { get; }
  bool ShowUserPasswordInputs { get; }
  IAsyncRelayCommand SignInCommand { get; }
  IRelayCommand SignOutCommand { get; }
  string StatusMessage { get; }
  string TwoFactorCode { get; set; }
  ControlrViewerOptions ViewerOptions { get; }

  void RegisterAuthChangeHandler(Guid viewerInstanceId);
}

public partial class MainWindowViewModel : ObservableObject, IMainWindowViewModel
{
  private IControlrAuthSession? _authSession;
  private Guid? _viewerInstanceId;

  public MainWindowViewModel(ControlrViewerOptions viewerOptions)
  {
    ViewerOptions = viewerOptions;
    ActivePage = ViewerPage.RemoteControl;
    DeviceIdText = viewerOptions.DeviceId.ToString();
    IsDarkMode = true;
    IsInteractiveBearerAuth = viewerOptions.AuthenticationMethod == ViewerAuthenticationMethod.InteractiveBearer;
    PersonalAccessToken = viewerOptions.PersonalAccessToken ?? string.Empty;
    ServerUrl = viewerOptions.BaseUrl.ToString();
  }

  [ObservableProperty]
  public partial ViewerPage ActivePage { get; set; }
  [ObservableProperty]
  public partial string DeviceIdText { get; set; }
  [ObservableProperty]
  public partial string Email { get; set; } = string.Empty;
  [ObservableProperty]
  public partial bool IsBusy { get; set; }
  [ObservableProperty]
  public partial bool IsDarkMode { get; set; } = true;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(ShowPersonalAccessTokenInput))]
  [NotifyPropertyChangedFor(nameof(ShowUserPasswordInputs))]
  [NotifyPropertyChangedFor(nameof(LoginDescription))]
  [NotifyPropertyChangedFor(nameof(LoginTitle))]
  public partial bool IsInteractiveBearerAuth { get; set; }
  public bool IsLoginVisible => !IsViewerVisible;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsLoginVisible))]
  public partial bool IsViewerVisible { get; set; }
  public string LoginDescription => ShowPersonalAccessTokenInput
    ? "Enter the ControlR server, device ID, and a personal access token generated from the web app."
    : RequiresPasswordChange
      ? "Enter your current password and a new password. The new password will be applied immediately."
      : "Enter the ControlR server, device ID, and the same email and password you use in the web app. The OTP field only appears when the account requires two-factor authentication.";
  public string LoginTitle => ShowPersonalAccessTokenInput
    ? "Connect with a personal access token"
    : RequiresPasswordChange
      ? "Change your password"
      : "Sign in to the viewer";
  [ObservableProperty]
  public partial string NewPassword { get; set; } = string.Empty;
  [ObservableProperty]
  public partial string Password { get; set; } = string.Empty;
  [ObservableProperty]
  public partial string PersonalAccessToken { get; set; } = string.Empty;
  [ObservableProperty]
  public partial bool RequiresPasswordChange { get; set; }
  [ObservableProperty]
  public partial bool RequiresTwoFactor { get; set; }
  [ObservableProperty]
  public partial string ServerUrl { get; set; }
  public bool ShowPersonalAccessTokenInput => !IsInteractiveBearerAuth;
  public bool ShowUserPasswordInputs => IsInteractiveBearerAuth;
  [ObservableProperty]
  public partial string StatusMessage { get; set; } = string.Empty;
  [ObservableProperty]
  public partial string TwoFactorCode { get; set; } = string.Empty;
  public ControlrViewerOptions ViewerOptions { get; }

  public void Dispose()
  {
    _authSession?.StateChanged -= HandleSessionStateChanged;
    GC.SuppressFinalize(this);
  }

  public void RegisterAuthChangeHandler(Guid viewerInstanceId)
  {
    if (_viewerInstanceId == viewerInstanceId && _authSession is not null)
    {
      var logger = ViewerRegistry.GetService<ILogger<MainWindowViewModel>>(viewerInstanceId);
      logger?.LogWarning("Attempted to register auth change handler for viewer instance {InstanceId}, but a handler is already registered.", viewerInstanceId);
      return;
    }

    _authSession?.StateChanged -= HandleSessionStateChanged;

    var authSession = ViewerRegistry.GetAuthSession(viewerInstanceId);
    
    _viewerInstanceId = viewerInstanceId;
    _authSession = authSession;
    _authSession.SetBaseUrl(ViewerOptions.BaseUrl);
    _authSession.StateChanged += HandleSessionStateChanged;
  }

  private async void HandleSessionStateChanged(object? sender, ControlrAuthSessionStateChangedEventArgs e)
  {
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      if (!IsInteractiveBearerAuth)
      {
        IsViewerVisible = !string.IsNullOrWhiteSpace(ViewerOptions.PersonalAccessToken);
        RequiresTwoFactor = false;
        TwoFactorCode = string.Empty;

        if (!IsViewerVisible && !string.IsNullOrWhiteSpace(e.Message))
        {
          StatusMessage = e.Message;
        }

        return;
      }

      switch (e.State)
      {
        case ControlrAuthSessionState.Authenticated:
          IsViewerVisible = true;
          RequiresPasswordChange = false;
          RequiresTwoFactor = false;
          TwoFactorCode = string.Empty;
          break;
        case ControlrAuthSessionState.AwaitingPasswordChange:
          IsViewerVisible = false;
          RequiresPasswordChange = true;
          RequiresTwoFactor = false;
          TwoFactorCode = string.Empty;
          StatusMessage = e.Message ?? "A password change is required.";
          break;
        case ControlrAuthSessionState.AwaitingTwoFactor:
          IsViewerVisible = false;
          RequiresPasswordChange = false;
          RequiresTwoFactor = true;
          StatusMessage = e.Message ?? "Two-factor authentication is enabled.";
          break;
        case ControlrAuthSessionState.Expired:
          IsViewerVisible = false;
          RequiresPasswordChange = false;
          RequiresTwoFactor = false;
          TwoFactorCode = string.Empty;
          StatusMessage = e.Message ?? "The session expired. Sign in again.";
          break;
        default:
          IsViewerVisible = false;
          RequiresPasswordChange = false;
          RequiresTwoFactor = false;
          TwoFactorCode = string.Empty;
          break;
      }
    });
  }

  partial void OnIsInteractiveBearerAuthChanged(bool value)
  {
    // In case this fires in the constructor before ViewerOptions is set.
    if (ViewerOptions is null)
    {
      return;
    }

    ViewerOptions.AuthenticationMethod = value
      ? ViewerAuthenticationMethod.InteractiveBearer
      : ViewerAuthenticationMethod.PersonalAccessToken;

    RequiresPasswordChange = false;
    RequiresTwoFactor = false;
    NewPassword = string.Empty;
    TwoFactorCode = string.Empty;
    StatusMessage = string.Empty;

    if (_authSession is null)
    {
      return;
    }

    ViewerOptions.PersonalAccessToken = null;
    _authSession.SignOut().GetAwaiter().GetResult();
  }

  [RelayCommand]
  private async Task SignIn(CancellationToken cancellationToken)
  {
    if (!TryApplyViewerConnectionSettings(out var validationError))
    {
      StatusMessage = validationError;
      return;
    }

    if (_authSession is null)
    {
      StatusMessage = "Viewer services are not initialized yet.";
      return;
    }

    IsBusy = true;
    StatusMessage = string.Empty;

    try
    {
      if (ShowPersonalAccessTokenInput)
      {
        var token = PersonalAccessToken.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
          StatusMessage = "Enter a personal access token.";
          return;
        }

        ViewerOptions.AuthenticationMethod = ViewerAuthenticationMethod.PersonalAccessToken;
        ViewerOptions.PersonalAccessToken = token;
        _authSession.SetPersonalAccessToken(token);
        IsViewerVisible = true;
        StatusMessage = "Connecting with personal access token.";
        return;
      }

      ViewerOptions.AuthenticationMethod = ViewerAuthenticationMethod.InteractiveBearer;

      if (RequiresPasswordChange)
      {
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
          StatusMessage = "Enter a new password.";
          return;
        }

        var newPassword = NewPassword;
        var twoFactorCode = string.IsNullOrWhiteSpace(TwoFactorCode)
          ? null
          : TwoFactorCode;
        var changeResult = await _authSession.ChangePasswordWithCredentials(
          Email.Trim(),
          Password,
          newPassword,
          twoFactorCode,
          cancellationToken);

        if (!changeResult.IsSuccess)
        {
          StatusMessage = changeResult.Reason ?? "Password change failed.";
          return;
        }

        NewPassword = string.Empty;
        StatusMessage = "Password changed. Signing in...";

        ViewerOptions.PersonalAccessToken = null;

        var signInResult = await _authSession.SignIn(
          new InteractiveSignInRequest
          {
            Email = Email.Trim(),
            Password = newPassword,
            TwoFactorCode = twoFactorCode
          },
          cancellationToken);
        StatusMessage = signInResult.Message ?? string.Empty;

        return;
      }

      if (!RequiresTwoFactor)
      {
        ViewerOptions.PersonalAccessToken = null;
        _authSession.SetPersonalAccessToken(null);
      }

      if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
      {
        StatusMessage = "Enter the account email and password.";
        return;
      }

      var result = await _authSession.SignIn(
        new InteractiveSignInRequest
        {
          Email = Email.Trim(),
          Password = Password,
          TwoFactorCode = RequiresTwoFactor ? TwoFactorCode.Replace(" ", string.Empty) : null
        },
        cancellationToken);

      StatusMessage = result.Message ?? string.Empty;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      StatusMessage = "Sign-in was canceled.";
    }
    catch (Exception ex)
    {
      StatusMessage = $"Sign-in failed: {ex.Message}";
    }
    finally
    {
      IsBusy = false;
    }
  }

  [RelayCommand]
  private void SignOut()
  {
    if (_authSession is null)
    {
      StatusMessage = "Viewer services are not initialized yet.";
      return;
    }

    ViewerOptions.PersonalAccessToken = null;
    _ = _authSession.SignOut();
    IsViewerVisible = false;
    StatusMessage = "Signed out.";
  }

  private bool TryApplyViewerConnectionSettings(out string validationError)
  {
    if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var baseUrl))
    {
      validationError = "Enter a valid absolute server URL.";
      return false;
    }

    if (!Guid.TryParse(DeviceIdText, out var deviceId))
    {
      validationError = "Enter a valid device ID.";
      return false;
    }

    ViewerOptions.BaseUrl = baseUrl;
    ViewerOptions.DeviceId = deviceId;
    _authSession?.SetBaseUrl(baseUrl);
    validationError = string.Empty;
    return true;
  }
}
