using ControlR.Libraries.Signalr.Client.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.Signalr.Client.Tests;

/// <summary>
/// Pins which protocol the shared client builder proposes, using the production registration and
/// connect path against a real TestServer.  A client speaks exactly one protocol, so a server that
/// does not offer the one the client proposes fails the handshake outright.  The server reports the
/// negotiated transfer format, which the protocol decides: Text for JSON, Binary for MessagePack.
/// </summary>
public class ProtocolNegotiationTests
{
  private static readonly Uri _hubEndpoint = new("http://localhost/testhub");

  [Fact]
  public async Task Connect_ThroughProductionBuilder_NegotiatesMessagePack()
  {
    await using var server = await StartServerAsync(json: true, messagePack: true);
    await using var client = CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    Assert.True(await ConnectAsync(client, server, cancellation.Token));
    Assert.Equal(TransferFormat.Binary.ToString(), await client.Server.GetNegotiatedFormat());
  }

  [Fact]
  public async Task Connect_WhenServerOffersJsonOnly_ReturnsFalse()
  {
    await using var server = await StartServerAsync(json: true, messagePack: false);
    await using var client = CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    Assert.False(await ConnectAsync(client, server, cancellation.Token));
  }

  [Fact]
  public async Task Connect_WhenServerOffersMessagePackOnly_ReturnsTrue()
  {
    await using var server = await StartServerAsync(json: false, messagePack: true);
    await using var client = CreateClient();
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    Assert.True(await ConnectAsync(client, server, cancellation.Token));
  }

  private static async Task<bool> ConnectAsync(
    IHubConnection<ITestHub> client,
    WebApplication server,
    CancellationToken cancellationToken)
  {
    return await client.Connect(
      _hubEndpoint,
      autoRetry: false,
      configure: options =>
      {
        options.HttpMessageHandlerFactory = _ => server.GetTestServer().CreateHandler();
        options.Transports = HttpTransportType.LongPolling;
      },
      cancellationToken: cancellationToken);
  }

  private static IHubConnection<ITestHub> CreateClient()
  {
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddStronglyTypedSignalrClient<ITestHub, ITestClient, TestClientImpl>(ServiceLifetime.Singleton);
    return services.BuildServiceProvider().GetRequiredService<IHubConnection<ITestHub>>();
  }

  private static void RemoveProtocol<TProtocol>(IServiceCollection services)
    where TProtocol : class, IHubProtocol
  {
    var descriptors = services
      .Where(x => x.ServiceType == typeof(IHubProtocol) && x.ImplementationType == typeof(TProtocol))
      .ToList();

    foreach (var descriptor in descriptors)
    {
      services.Remove(descriptor);
    }
  }

  private static async Task<WebApplication> StartServerAsync(bool json, bool messagePack)
  {
    var builder = WebApplication.CreateBuilder();
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.WebHost.UseTestServer();

    var signalR = builder.Services.AddSignalR();
    if (json)
    {
      signalR.AddJsonProtocol();
    }
    else
    {
      RemoveProtocol<JsonHubProtocol>(builder.Services);
    }

    if (messagePack)
    {
      signalR.AddMessagePackProtocol();
    }
    else
    {
      RemoveProtocol<MessagePackHubProtocol>(builder.Services);
    }

    var app = builder.Build();
    app.MapHub<TestHub>("/testhub");
    await app.StartAsync();
    return app;
  }
}

public interface ITestClient
{
  Task Ping();
}

public interface ITestHub
{
  Task<string> GetNegotiatedFormat();
}

public sealed class TestClientImpl : ITestClient
{
  public Task Ping() => Task.CompletedTask;
}

public sealed class TestHub : Hub<ITestClient>
{
  public Task<string> GetNegotiatedFormat()
  {
    var transferFormat = Context.Features.Get<ITransferFormatFeature>();
    return Task.FromResult(transferFormat?.ActiveFormat.ToString() ?? "unknown");
  }
}
