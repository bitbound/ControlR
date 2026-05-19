using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using ControlR.ApiClient;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Viewer.Common.Options;
using ControlR.Viewer.Avalonia.Services;
using ControlR.Viewer.Avalonia.Services.Navigation;

namespace ControlR.AvaloniaViewerExample.ViewModels;

public interface IMainWindowViewModel : INotifyPropertyChanged, IDisposable
{
  ViewerPage ActivePage { get; set; }
  string DeviceIdText { get; set; }
  string Email { get; set; }
  bool IsBusy { get; }
  bool IsDarkMode { get; set; }
  bool IsLoginVisible { get; }
  bool IsViewerVisible { get; }
  string Password { get; set; }
  bool RequiresTwoFactor { get; }
  string ServerUrl { get; set; }
  IAsyncRelayCommand SignInCommand { get; }
  IRelayCommand SignOutCommand { get; }
  string StatusMessage { get; }
  string TwoFactorCode { get; set; }
  ControlrViewerOptions ViewerOptions { get; }

  void AttachViewer(Guid viewerInstanceId);
}

public partial class MainWindowViewModel : ObservableObject, IMainWindowViewModel
{
  private IControlrAuthSession? _authSession;
  private Guid? _viewerInstanceId;

  public MainWindowViewModel(ControlrViewerOptions viewerOptions)
  {
    ActivePage = ViewerPage.RemoteControl;
    DeviceIdText = viewerOptions.DeviceId.ToString();
    IsDarkMode = true;
    ServerUrl = viewerOptions.BaseUrl.ToString();
    ViewerOptions = viewerOptions;
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
  public bool IsLoginVisible => !IsViewerVisible;
  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(IsLoginVisible))]
  public partial bool IsViewerVisible { get; set; }
  [ObservableProperty]
  public partial string Password { get; set; } = string.Empty;
  [ObservableProperty]
  public partial bool RequiresTwoFactor { get; set; }
  [ObservableProperty]
  public partial string ServerUrl { get; set; }
  [ObservableProperty]
  public partial string StatusMessage { get; set; } = string.Empty;
  [ObservableProperty]
  public partial string TwoFactorCode { get; set; } = string.Empty;
  public ControlrViewerOptions ViewerOptions { get; }

  public void AttachViewer(Guid viewerInstanceId)
  {
    if (_viewerInstanceId == viewerInstanceId && _authSession is not null)
    {
      return;
    }

    if (_authSession is not null)
    {
      _authSession.StateChanged -= HandleSessionStateChanged;
    }

    var authSession = ViewerRegistry.GetService<IControlrAuthSession>(viewerInstanceId);
    if (authSession is null)
    {
      return;
    }

    _viewerInstanceId = viewerInstanceId;
    _authSession = authSession;
    _authSession.SetBaseUrl(ViewerOptions.BaseUrl);
    _authSession.StateChanged += HandleSessionStateChanged;
  }

  public void Dispose()
  {
    if (_authSession is not null)
    {
      _authSession.StateChanged -= HandleSessionStateChanged;
    }

    GC.SuppressFinalize(this);
  }

  private async void HandleSessionStateChanged(object? sender, ControlrAuthSessionStateChangedEventArgs e)
  {
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      switch (e.State)
      {
        case ControlrAuthSessionState.Authenticated:
          IsViewerVisible = true;
          RequiresTwoFactor = false;
          TwoFactorCode = string.Empty;
          break;
        case ControlrAuthSessionState.AwaitingTwoFactor:
          IsViewerVisible = false;
          RequiresTwoFactor = true;
          break;
        case ControlrAuthSessionState.Expired:
          IsViewerVisible = false;
          RequiresTwoFactor = false;
          TwoFactorCode = string.Empty;
          StatusMessage = e.Message ?? "The session expired. Sign in again.";
          break;
        default:
          IsViewerVisible = false;
          RequiresTwoFactor = false;
          TwoFactorCode = string.Empty;
          break;
      }
    });
  }

  [RelayCommand]
  private async Task SignIn(CancellationToken cancellationToken)
  {
    if (!TryApplyViewerConnectionSettings(out var validationError))
    {
      StatusMessage = validationError;
      return;
    }

    if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
    {
      StatusMessage = "Enter the account email and password.";
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
      var result = RequiresTwoFactor
        ? await _authSession.SubmitTwoFactorCode(TwoFactorCode.Replace(" ", string.Empty), cancellationToken)
        : await _authSession.SignIn(Email.Trim(), Password, cancellationToken);

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

    _ = _authSession.SignOut();
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
