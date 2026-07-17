using ControlR.Libraries.TestingUtilities.Logging;
using ControlR.Web.Server.Services.ServiceAccounts;
using ControlR.Web.Server.Startup;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace ControlR.Web.Server.Tests.V0;

public class ServiceAccountSecretLoggingTests(ITestOutputHelper testOutput)
{
  private CapturingLoggerProvider _loggerProvider = null!;

  [Fact]
  public async Task AddCredential_NeverLogsPlaintextSecret()
  {
    _loggerProvider = new CapturingLoggerProvider();

    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      configureLogging: builder => builder.AddProvider(_loggerProvider));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var createResult = await manager.CreateForServer(
      "AddCred Secret Log Test SA",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);
    var accountId = createResult.Value.ServiceAccount.Id;

    var addCredResult = await manager.AddCredential(
      accountId,
      "Secret Log Test Credential",
      TestContext.Current.CancellationToken);
    Assert.True(addCredResult.IsSuccess);

    var plainSecret = addCredResult.Value.PlainTextSecretKey;
    Assert.NotEmpty(plainSecret);

    var capturedMessages = _loggerProvider.Logs.Select(l => l.Message).ToList();

    foreach (var message in capturedMessages)
    {
      Assert.DoesNotContain(plainSecret, message);
    }
    var exceptionMessages = _loggerProvider.Logs.Select(l => l.ExceptionMessage).Where(m => m != null).Cast<string>();
    foreach (var message in exceptionMessages)
    {
      Assert.DoesNotContain(plainSecret, message);
    }
  }

  [Fact]
  public async Task CreateForServer_NeverLogsPlaintextSecret()
  {
    _loggerProvider = new CapturingLoggerProvider();

    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      configureLogging: builder => builder.AddProvider(_loggerProvider));

    using var scope = testApp.CreateScope();
    var manager = scope.ServiceProvider.GetRequiredService<IServiceAccountManager>();

    var createResult = await manager.CreateForServer(
      "Secret Log Test SA",
      null,
      TestContext.Current.CancellationToken);
    Assert.True(createResult.IsSuccess);

    var plainSecret = createResult.Value.PlainTextSecretKey;
    Assert.NotEmpty(plainSecret);

    var capturedMessages = _loggerProvider.Logs.Select(l => l.Message).ToList();

    foreach (var message in capturedMessages)
    {
      Assert.DoesNotContain(plainSecret, message);
    }
    var exceptionMessages = _loggerProvider.Logs.Select(l => l.ExceptionMessage).Where(m => m != null).Cast<string>();
    foreach (var message in exceptionMessages)
    {
      Assert.DoesNotContain(plainSecret, message);
    }
  }

  [Fact]
  public async Task ServiceAccountConfig_NeverLogsPlaintextSecretDuringBootstrap()
  {
    _loggerProvider = new CapturingLoggerProvider();

    var config = new Dictionary<string, string?>
    {
      ["Bootstrap:ServerServiceAccountName"] = "BootSecret Test",
      ["Bootstrap:ServerServiceAccountTokenId"] = "2a24478c-a43a-4d3b-a95a-350f496ce268",
      ["Bootstrap:ServerServiceAccountTokenSecret"] = "BootSecretConfiguration1234567890abcdef",
    };

    await using var testApp = await TestAppBuilder.CreateTestApp(
      testOutput,
      extraConfiguration: config,
      configureLogging: builder => builder.AddProvider(_loggerProvider));

    await testApp.App.BootstrapServerServiceAccount(TestContext.Current.CancellationToken);

    var plaintextSecret = config["Bootstrap:ServerServiceAccountTokenSecret"]!;
    var capturedMessages = _loggerProvider.Logs.Select(l => l.Message).ToList();

    foreach (var message in capturedMessages)
    {
      Assert.DoesNotContain(plaintextSecret, message);
    }
    var exceptionMessages = _loggerProvider.Logs.Select(l => l.ExceptionMessage).Where(m => m != null).Cast<string>();
    foreach (var message in exceptionMessages)
    {
      Assert.DoesNotContain(plaintextSecret, message);
    }
  }
}