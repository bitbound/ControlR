using System.Net;
using ControlR.ApiClient;
using ControlR.ApiClient.Auth;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Drives <see cref="IControlrApiClientFactory"/> against a real test server. The factory's unit
/// tests run it over fake handlers, which covers its own bookkeeping but never proves that a
/// client it built can actually authenticate and complete a call. These close that gap.
/// </summary>
public class ControlrApiClientFactoryIntegrationTests(ITestOutputHelper testOutput)
{
  private const string TargetName = "tenant-a";

  private static readonly Uri _baseUrl = new("http://localhost");

  private static readonly IReadOnlyDictionary<string, string?> _interactiveBearerSettings =
    new Dictionary<string, string?>
    {
      ["AppOptions:EnableInteractiveBearerLogin"] = "true"
    };

  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task GetOrCreateAuthSession_WhenTargetGoesIdlePastItsLifetime_KeepsTheSignedInTargetUsable()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(
      _testOutput,
      settings: _interactiveBearerSettings);

    var tenant = await testServer.Services.CreateTestTenant();
    var user = await testServer.Services.CreateTestUser(tenant.Id, "factory-interactive@t.local");

    using var factory = CreateFactory(
      testServer,
      options =>
      {
        options.MaxIdleClientLifetime = TimeSpan.FromMinutes(30);
        // Long enough that the sweep under test is the explicit call, not the periodic timer.
        options.SweeperInterval = TimeSpan.FromHours(1);
      });

    var client = factory.GetOrCreateClient(TargetName, options => options.BaseUrl = _baseUrl);
    var session = factory.GetOrCreateAuthSession(TargetName);

    var signIn = await session.SignIn(
      new InteractiveSignInRequest
      {
        Email = user.Email ?? throw new InvalidOperationException("The test user must have an email address."),
        Password = "T3stP@ssw0rd!"
      },
      TestContext.Current.CancellationToken);

    Assert.Equal(InteractiveLoginStatus.Authenticated, signIn.Status);

    var beforeIdle = await client.Internal.Auth.GetManageInfo(TestContext.Current.CancellationToken);
    Assert.True(beforeIdle.IsSuccess, beforeIdle.ToString());

    // Nothing calls the factory from here on, so the target is idle by the factory's own definition.
    // Its background token refresh does not count as use, which is what used to let the sweep destroy
    // a login that was still perfectly good.
    testServer.TimeProvider.Advance(TimeSpan.FromMinutes(31));
    factory.SweepIdleClients();

    Assert.Equal([TargetName], factory.GetClientNames());
    Assert.True(factory.TryGetAuthSession(TargetName, out var probed));
    Assert.Same(session, probed);
    Assert.Equal(ControlrAuthSessionState.Authenticated, session.State);

    // Past the bearer token's own lifetime, so the next call has to renew it against the real server.
    // A target that survived the sweep but lost its transport or its session would fail here rather
    // than merely looking present in GetClientNames.
    testServer.TimeProvider.Advance(TimeSpan.FromMinutes(61));

    var afterIdle = await client.Internal.Auth.GetManageInfo(TestContext.Current.CancellationToken);

    Assert.True(afterIdle.IsSuccess, afterIdle.ToString());
    Assert.NotNull(afterIdle.Value);
    Assert.Equal(user.Email, afterIdle.Value.Email);
  }

  [Fact]
  public async Task GetOrCreateClient_WhenServerServiceAccountKeyIsConfigured_CompletesAServerScopedCall()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var apiKey = await CreateServerServiceAccountKey(testServer, "factory-server-sa");

    using var factory = CreateFactory(testServer);
    var client = factory.GetOrCreateClient(
      TargetName,
      options =>
      {
        options.BaseUrl = _baseUrl;
        options.ServiceAccountApiKey = apiKey;
      });

    var result = await client.V1.Tenants.CreateTenant(
      new V1Dtos.CreateTenantRequestDto("Factory Tenant"),
      TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess, result.ToString());
    Assert.NotNull(result.Value);
    Assert.NotEqual(Guid.Empty, result.Value.TenantId);
  }

  [Fact]
  public async Task GetOrCreateClient_WhenTenantServiceAccountKeyIsConfigured_AuthenticatesWithoutServerScope()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var tenant = await testServer.Services.CreateTestTenant();
    var apiKey = await CreateTenantServiceAccountKey(testServer, tenant.Id, "factory-tenant-sa");

    using var factory = CreateFactory(testServer);
    var client = factory.GetOrCreateClient(
      TargetName,
      options =>
      {
        options.BaseUrl = _baseUrl;
        options.ServiceAccountApiKey = apiKey;
      });

    var result = await client.V1.Tenants.CreateTenant(
      new V1Dtos.CreateTenantRequestDto("Denied Tenant"),
      TestContext.Current.CancellationToken);

    // Creating a tenant is server-scoped, so a tenant credential must not succeed. What this test is
    // really asserting is that the credential authenticated: 403 is an authorized-decision on a known
    // principal, whereas 401 would mean the composite key was never accepted, which is
    // indistinguishable from a client that sent no header at all.
    Assert.False(result.IsSuccess);
    Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
  }

  private static ControlrApiClientFactory CreateFactory(
    TestWebServer testServer,
    Action<ControlrApiClientFactoryOptions>? configure = null)
  {
    var options = new ControlrApiClientFactoryOptions
    {
      // The factory requires a new handler per call, and TestServer.CreateHandler() satisfies that:
      // each of the target's two clients gets its own route into the test server.
      HttpMessageHandlerFactory = () => testServer.TestServer.CreateHandler(),
      MaxIdleClientLifetime = null
    };
    configure?.Invoke(options);

    return new ControlrApiClientFactory(
      options,
      testServer.TimeProvider,
      NullLoggerFactory.Instance);
  }

  private static async Task<string> CreateServerServiceAccountKey(TestWebServer testServer, string name)
  {
    var manager = testServer.Services.GetRequiredService<IServiceAccountManager>();

    var account = await manager.CreateForServer(
      name,
      null,
      ServiceAccountAccessMode.Unrestricted,
      TestContext.Current.CancellationToken);

    Assert.True(account.IsSuccess, account.ToString());

    var credential = await manager.AddCredentialForServer(
      account.Value.Id,
      "factory-credential",
      expiresAt: null,
      TestActors.User(),
      TestContext.Current.CancellationToken);

    Assert.True(credential.IsSuccess, credential.ToString());
    return credential.Value.PlainTextSecretKey;
  }

  private static async Task<string> CreateTenantServiceAccountKey(
    TestWebServer testServer,
    Guid tenantId,
    string name)
  {
    var manager = testServer.Services.GetRequiredService<IServiceAccountManager>();

    var account = await manager.CreateForTenant(
      name,
      null,
      tenantId,
      TestActors.User(),
      TestContext.Current.CancellationToken);

    Assert.True(account.IsSuccess, account.ToString());

    var credential = await manager.AddCredentialForTenant(
      account.Value.Id,
      tenantId,
      "factory-credential",
      expiresAt: null,
      TestActors.User(),
      TestContext.Current.CancellationToken);

    Assert.True(credential.IsSuccess, credential.ToString());
    return credential.Value.PlainTextSecretKey;
  }
}
