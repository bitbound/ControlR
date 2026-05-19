using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Viewer.Common.Options;
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
}

public partial class MainWindowViewModel(ControlrViewerOptions viewerOptions) : ObservableObject, IMainWindowViewModel
{
  private const string DesktopLoginEndpoint = "/api/auth/desktop-login";
  private const string RefreshEndpoint = "/api/auth/refresh";

  private static readonly TimeSpan _refreshLeadTime = TimeSpan.FromMinutes(1);

  private readonly HttpClient _httpClient = new();
  private readonly TimeProvider _timeProvider = TimeProvider.System;

  private CancellationTokenSource? _refreshLoopCts;

  [ObservableProperty]
  public partial ViewerPage ActivePage { get; set; } = ViewerPage.RemoteControl;
  [ObservableProperty]
  public partial string DeviceIdText { get; set; } = viewerOptions.DeviceId.ToString();
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
  public partial string ServerUrl { get; set; } = viewerOptions.BaseUrl.ToString();
  [ObservableProperty]
  public partial string StatusMessage { get; set; } = string.Empty;
  [ObservableProperty]
  public partial string TwoFactorCode { get; set; } = string.Empty;
  public ControlrViewerOptions ViewerOptions { get; } = viewerOptions;

  public void Dispose()
  {
    StopRefreshLoop();
    _httpClient.Dispose();
  }

  private async Task HandleRefreshLoopFault(string message)
  {
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
      StopRefreshLoop();
      ViewerOptions.Auth.ClearBearerTokens();
      IsViewerVisible = false;
      RequiresTwoFactor = false;
      TwoFactorCode = string.Empty;
      StatusMessage = message;
    });
  }

  private async Task<string> ReadErrorMessage(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    if (!string.IsNullOrWhiteSpace(content))
    {
      return content;
    }

    return $"The server returned {(int)response.StatusCode} {response.ReasonPhrase}.";
  }

  private async Task RefreshSession(CancellationToken cancellationToken)
  {
    var auth = ViewerOptions.Auth;
    if (!auth.CanRefreshBearerToken || string.IsNullOrWhiteSpace(auth.RefreshToken))
    {
      return;
    }

    await auth.BearerRefreshLock.WaitAsync(cancellationToken);
    try
    {
      if (!auth.ShouldRefreshBearerToken(_timeProvider, _refreshLeadTime) || string.IsNullOrWhiteSpace(auth.RefreshToken))
      {
        return;
      }

      using var response = await _httpClient.PostAsJsonAsync(
        new Uri(ViewerOptions.BaseUrl, RefreshEndpoint),
        new RefreshTokenRequestDto(auth.RefreshToken),
        cancellationToken);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        throw new InvalidOperationException("The refresh token is no longer valid.");
      }

      response.EnsureSuccessStatusCode();

      var refreshResponse = await response.Content.ReadFromJsonAsync<AccessTokenResponseDto>(cancellationToken) ??
        throw new InvalidOperationException("The refresh response was empty.");

      auth.SetBearerTokenResponse(refreshResponse, _timeProvider);
    }
    finally
    {
      auth.BearerRefreshLock.Release();
    }
  }

  private async Task RunRefreshLoop(CancellationToken cancellationToken)
  {
    try
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        var expiresAt = ViewerOptions.Auth.BearerTokenExpiresAt;
        if (expiresAt is null)
        {
          return;
        }

        var delay = expiresAt.Value - _timeProvider.GetUtcNow() - _refreshLeadTime;
        if (delay < TimeSpan.Zero)
        {
          delay = TimeSpan.Zero;
        }

        await Task.Delay(delay, cancellationToken);
        await RefreshSession(cancellationToken);
      }
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
      await HandleRefreshLoopFault($"Session refresh failed: {ex.Message}");
    }
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

    IsBusy = true;
    StatusMessage = string.Empty;

    try
    {
      using var response = await _httpClient.PostAsJsonAsync(
        new Uri(ViewerOptions.BaseUrl, DesktopLoginEndpoint),
        new LoginRequestDto(
          Email.Trim(),
          Password,
          string.IsNullOrWhiteSpace(TwoFactorCode) ? null : TwoFactorCode.Replace(" ", string.Empty),
          null),
        cancellationToken);

      if (response.StatusCode == HttpStatusCode.Unauthorized)
      {
        StatusMessage = RequiresTwoFactor
          ? "The two-factor code was rejected."
          : "Email or password was not accepted.";
        return;
      }

      if (!response.IsSuccessStatusCode)
      {
        StatusMessage = await ReadErrorMessage(response, cancellationToken);
        return;
      }

      var loginResponse = await response.Content.ReadFromJsonAsync<DesktopLoginResponseDto>(cancellationToken) ??
        throw new InvalidOperationException("The login response was empty.");

      if (loginResponse.RequiresTwoFactor)
      {
        RequiresTwoFactor = true;
        StatusMessage = "Two-factor authentication is enabled. Enter your authenticator code to continue.";
        return;
      }

      if (loginResponse.Tokens is null)
      {
        StatusMessage = "The server did not return tokens.";
        return;
      }

      ViewerOptions.Auth.SetBearerTokenResponse(loginResponse.Tokens, _timeProvider);
      RequiresTwoFactor = false;
      TwoFactorCode = string.Empty;
      IsViewerVisible = true;
      StatusMessage = "Connected.";
      StartRefreshLoop();
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
    StopRefreshLoop();
    ViewerOptions.Auth.ClearBearerTokens();
    IsViewerVisible = false;
    RequiresTwoFactor = false;
    TwoFactorCode = string.Empty;
    StatusMessage = "Signed out.";
  }

  private void StartRefreshLoop()
  {
    StopRefreshLoop();
    if (!ViewerOptions.Auth.CanRefreshBearerToken)
    {
      return;
    }

    _refreshLoopCts = new CancellationTokenSource();
    _ = RunRefreshLoop(_refreshLoopCts.Token);
  }

  private void StopRefreshLoop()
  {
    _refreshLoopCts?.Cancel();
    _refreshLoopCts?.Dispose();
    _refreshLoopCts = null;
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
    validationError = string.Empty;
    return true;
  }
}
