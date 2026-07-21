using ControlR.Web.Server.Data;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Startup;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.V1;

public class BootstrapServiceAccountTests(ITestOutputHelper testOutput)
{
  private const string AccountName = "test-bootstrap-sa";
  private const string TokenId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
  private const string TokenSecret = "test-bootstrap-secret-key-thirty-two-chars!";

  private readonly ITestOutputHelper _testOutputHelper = testOutput;

  [Fact]
  public async Task Bootstrap_CreatesServerServiceAccount()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
      ["Bootstrap:ServerServiceAccountTokenId"] = TokenId,
      ["Bootstrap:ServerServiceAccountTokenSecret"] = TokenSecret,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await testApp.App.BootstrapServerServiceAccount(TestContext.Current.CancellationToken);

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var account = await appDb.ServiceAccounts
      .IgnoreQueryFilters()
      .Include(x => x.Credentials)
      .FirstOrDefaultAsync(x => x.Name == AccountName, TestContext.Current.CancellationToken);

    Assert.NotNull(account);
    Assert.Equal(ControlR.Web.Server.Data.Enums.ServiceAccountKind.Server, account.Kind);
    Assert.Null(account.TenantId);
    Assert.True(account.IsEnabled);

    Assert.NotEmpty(account.Credentials);
    Assert.Single(account.Credentials);
    var credential = account.Credentials[0];
    Assert.NotEqual(Guid.Empty, credential.Id);
    Assert.NotEmpty(credential.HashedSecret);
    Assert.Null(credential.RevokedAt);
  }

  [Fact]
  public async Task Bootstrap_CredentialCanAuthenticate()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
      ["Bootstrap:ServerServiceAccountTokenId"] = TokenId,
      ["Bootstrap:ServerServiceAccountTokenSecret"] = TokenSecret,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await testApp.App.BootstrapServerServiceAccount(TestContext.Current.CancellationToken);

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var credential = await appDb.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .FirstAsync(TestContext.Current.CancellationToken);

    var hexId = Convert.ToHexString(credential.Id.ToByteArray());
    var apiKey = $"{hexId}:{TokenSecret}";

    var serviceAccountManager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var validationResult = await serviceAccountManager.ValidateCredential(apiKey, TestContext.Current.CancellationToken);

    Assert.True(validationResult.IsSuccess);
    Assert.NotNull(validationResult.Value);
    Assert.Equal(credential.Id, validationResult.Value.Credential.Id);
    Assert.Equal(AccountName, validationResult.Value.ServiceAccount.Name);
  }

  [Fact]
  public async Task Bootstrap_SkipsWhenAccountAlreadyExists()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
      ["Bootstrap:ServerServiceAccountTokenId"] = TokenId,
      ["Bootstrap:ServerServiceAccountTokenSecret"] = TokenSecret,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await testApp.App.BootstrapServerServiceAccount(TestContext.Current.CancellationToken);
    await testApp.App.BootstrapServerServiceAccount(TestContext.Current.CancellationToken);

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var count = await appDb.ServiceAccounts
      .IgnoreQueryFilters()
      .CountAsync(x => x.Name == AccountName, TestContext.Current.CancellationToken);

    Assert.Equal(1, count);
  }

  [Fact]
  public async Task Bootstrap_WithKey_RawApiKeyHeaderRoundTrips()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
      ["Bootstrap:ServerServiceAccountTokenId"] = TokenId,
      ["Bootstrap:ServerServiceAccountTokenSecret"] = TokenSecret,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await testApp.App.BootstrapServerServiceAccount(TestContext.Current.CancellationToken);

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();
    var credential = await appDb.ServiceAccountCredentials
      .IgnoreQueryFilters()
      .FirstAsync(TestContext.Current.CancellationToken);

    var hexId = Convert.ToHexString(credential.Id.ToByteArray());
    Assert.Equal(32, hexId.Length);

    var configId = Guid.Parse(TokenId);
    Assert.Equal(configId, credential.Id);

    var apiKey = $"{hexId}:{TokenSecret}";
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();
    var result = await manager.ValidateCredential(apiKey, TestContext.Current.CancellationToken);
    Assert.True(result.IsSuccess);
  }

  [Fact]
  public async Task Bootstrap_WithNoConfig_DoesNothing()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper);
    await testApp.App.BootstrapServerServiceAccount(TestContext.Current.CancellationToken);

    using var scope = testApp.CreateScope();
    await using var appDb = scope.ServiceProvider.GetRequiredService<AppDb>();

    var count = await appDb.ServiceAccounts.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, count);
  }

  [Fact]
  public async Task Bootstrap_WithPartialConfig_Throws()
  {
    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = AccountName,
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutputHelper, extraConfiguration: config);
    await Assert.ThrowsAsync<InvalidOperationException>(
      () => testApp.App.BootstrapServerServiceAccount(TestContext.Current.CancellationToken));
  }

}