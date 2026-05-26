using ControlR.ApiClient;
using ControlR.ApiClient.Auth;
using ControlR.Libraries.Viewer.Common.Options;
using ControlR.Libraries.Viewer.Common.Services;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Tests;

public class ViewerConnectionAuthProviderTests
{
  [Fact]
  public async Task ConfigureSignalr_WhenUsingBearerAuth_UsesAccessTokenProvider()
  {
    var authSession = new StubAuthSession
    {
      AccessToken = "bearer-token"
    };

    var options = Microsoft.Extensions.Options.Options.Create(new ControlrViewerOptions
    {
      AuthenticationMethod = ViewerAuthenticationMethod.InteractiveBearer,
      BaseUrl = new Uri("https://controlr.example.com"),
      DeviceId = Guid.NewGuid()
    });

    var provider = new ViewerConnectionAuthProvider(authSession, options);
    var httpOptions = new HttpConnectionOptions();

    provider.ConfigureSignalr(httpOptions);

    Assert.NotNull(httpOptions.AccessTokenProvider);
    Assert.Equal("bearer-token", await httpOptions.AccessTokenProvider!());

    var headers = await provider.GetWebSocketHeaders(TestContext.Current.CancellationToken);
    Assert.Single(headers);
    Assert.Equal("Bearer bearer-token", headers[ControlrApiClientAuthState.AuthorizationHeader]);
  }

  [Fact]
  public void ConfigureSignalr_WhenUsingPersonalAccessToken_SetsPatHeader()
  {
    var options = Microsoft.Extensions.Options.Options.Create(new ControlrViewerOptions
    {
      AuthenticationMethod = ViewerAuthenticationMethod.PersonalAccessToken,
      BaseUrl = new Uri("https://controlr.example.com"),
      DeviceId = Guid.NewGuid(),
      PersonalAccessToken = "pat-token"
    });

    var provider = new ViewerConnectionAuthProvider(new StubAuthSession(), options);
    var httpOptions = new HttpConnectionOptions();

    provider.ConfigureSignalr(httpOptions);

    Assert.Equal("pat-token", httpOptions.Headers[ControlrApiClientOptions.PersonalAccessTokenHeader]);
    Assert.Null(httpOptions.AccessTokenProvider);
  }

  [Fact]
  public async Task GetWebSocketHeaders_WhenUsingPersonalAccessToken_ReturnsPatHeader()
  {
    var options = Microsoft.Extensions.Options.Options.Create(new ControlrViewerOptions
    {
      AuthenticationMethod = ViewerAuthenticationMethod.PersonalAccessToken,
      BaseUrl = new Uri("https://controlr.example.com"),
      DeviceId = Guid.NewGuid(),
      PersonalAccessToken = "pat-token"
    });

    var provider = new ViewerConnectionAuthProvider(new StubAuthSession(), options);

    var headers = await provider.GetWebSocketHeaders(TestContext.Current.CancellationToken);

    Assert.Single(headers);
    Assert.Equal("pat-token", headers[ControlrApiClientOptions.PersonalAccessTokenHeader]);
  }

  private sealed class StubAuthSession : IControlrAuthSession
  {
    public event EventHandler<ControlrAuthSessionStateChangedEventArgs>? StateChanged
    {
      add { }
      remove { }
    }

    public string? AccessToken { get; set; }
    public DateTimeOffset? AccessTokenExpiresAt => null;
    public Uri BaseUrl { get; private set; } = new("https://controlr.example.com");
    public bool IsAuthenticated => false;
    public string? PersonalAccessToken { get; private set; }
    public bool RequiresTwoFactor => false;
    public ControlrAuthSessionState State => ControlrAuthSessionState.SignedOut;

    public Task<ControlR.Libraries.Api.Contracts.Dtos.ApiResult> ChangePassword(string email, string currentPassword, string newPassword, string? twoFactorCode, CancellationToken cancellationToken = default)
    {
      throw new NotSupportedException();
    }

    public void Dispose()
    {
    }

    public Task<string?> GetAccessToken(CancellationToken cancellationToken = default) => Task.FromResult(AccessToken);

    public void SetBaseUrl(Uri baseUrl)
    {
      BaseUrl = baseUrl;
    }

    public void SetPersonalAccessToken(string? personalAccessToken)
    {
      PersonalAccessToken = personalAccessToken;
    }

    public Task<InteractiveLoginResult> SignIn(InteractiveSignInRequest request, CancellationToken cancellationToken = default)
    {
      throw new NotSupportedException();
    }

    public Task SignOut()
    {
      throw new NotSupportedException();
    }
  }
}