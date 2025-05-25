using System.Collections.Concurrent;
using ControlR.Agent.LoadTester;
using ControlR.Libraries.Shared.Helpers;
using ControlR.Libraries.Shared.Hubs;
using ControlR.Agent.LoadTester.Helpers;
using Microsoft.AspNetCore.SignalR.Client;
using ControlR.Libraries.Shared.Dtos.StreamerDtos;
using ControlR.Libraries.Shared.Interfaces.HubClients;
using ControlR.Libraries.Shared.Dtos.HubDtos;
using ControlR.Libraries.Shared.Primitives;

var agentCount = ArgsParser.GetArgValue<int>("--agent-count");
var startCount = ArgsParser.GetArgValue("--start-count", 0);
var serverUriArg = ArgsParser.GetArgValue<string>("--server-uri");
var serverUri = new Uri(serverUriArg);
var agentVersion = await GetAgentVersion(serverUri);
var tenantId = Guid.Parse("23fec81b-6e09-4161-92c3-930d8c5162ca");

Console.WriteLine($"Starting agent count at {startCount}.");

using var cts = new CancellationTokenSource();
var cancellationToken = cts.Token;

Console.CancelKeyPress += (s, e) => { cts.Cancel(); };

Console.WriteLine($"Connecting to {serverUri}");

var connections = new ConcurrentBag<HubConnection>();
var hubUri = new Uri(serverUri, "/hubs/agent");
var agentHubClient = new TestAgentHubClient();
var retryPolicy = new TestAgentRetryPolicy();

_ = ReportConnections(connections, cancellationToken);

await Parallel.ForAsync(startCount, startCount + agentCount, cts.Token, async (i, ct) =>
{
  if (ct.IsCancellationRequested)
  {
    return;
  }

  while (true)
  {
    try
    {
      var builder = new HubConnectionBuilder();
      var connection = builder
        .WithUrl(
          hubUri,
          options => ConnectionHelper.ConfigureHubConnection(i, options))
        .WithAutomaticReconnect(retryPolicy)
        .Build();

      connection.Reconnected += connectionId =>
      {
        Console.WriteLine($"Agent {i} reconnected with connection ID: {connectionId}");
        return Task.CompletedTask;
      };

      connection.Closed += error =>
      {
        Console.WriteLine($"Agent {i} connection closed: {error?.Message}");
        return Task.CompletedTask;
      };

      connection.Reconnecting += error =>
      {
        Console.WriteLine($"Agent {i} reconnecting: {error?.Message}");
        return Task.CompletedTask;
      };

      connection.On<StreamerSessionRequestDto, bool>(
        nameof(IAgentHubClient.CreateStreamingSession),
        agentHubClient.CreateStreamingSession);

      connection.On<TerminalSessionRequest, Result<TerminalSessionRequestResult>>(
        nameof(IAgentHubClient.CreateTerminalSession),
        agentHubClient.CreateTerminalSession);

      connection.On(
        nameof(IAgentHubClient.GetWindowsSessions),
         agentHubClient.GetWindowsSessions);

      connection.On<TerminalInputDto, Result>(
        nameof(IAgentHubClient.ReceiveTerminalInput),
        agentHubClient.ReceiveTerminalInput);

      connection.On<string>(
        nameof(IAgentHubClient.UninstallAgent),
        agentHubClient.UninstallAgent);

      await connection.StartAsync(ct);

      var deviceId = DeterministicGuid.Create(i);
      var deviceDto = await ConnectionHelper.CreateDevice(deviceId, tenantId, i, agentVersion);

      await connection.InvokeAsync(nameof(IAgentHub.UpdateDevice), deviceDto, ct);

      connections.Add(connection);
      break;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Agent {i} failed to connect: {ex.Message}");
      await Task.Delay(TimeSpan.FromSeconds(5), ct);
    }
  }
});

Console.WriteLine($"All {agentCount} agents started successfully.");
await Task.Delay(Timeout.Infinite, cancellationToken);
return;

static async Task ReportConnections(ConcurrentBag<HubConnection> connections, CancellationToken cancellationToken)
{
  using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
  while (await timer.WaitForNextTickAsync(cancellationToken))
  {
    var groups = connections.GroupBy(x => x.State);

    foreach (var group in groups)
    {
      Console.WriteLine($"{group.Key}: {group.Count()}");
    }
  }
}

static async Task<Version> GetAgentVersion(Uri serverUri)
{
  using var client = new HttpClient();
  while (true)
  {
    try
    {
      var version = await client.GetStringAsync(new Uri(serverUri, "/downloads/AgentVersion.txt"));
      return Version.Parse(version.Trim());
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error getting agent version: {ex.Message}");
      Console.WriteLine("Waiting for backend to be available.");
      await Task.Delay(TimeSpan.FromSeconds(3));
    }
  }
}
