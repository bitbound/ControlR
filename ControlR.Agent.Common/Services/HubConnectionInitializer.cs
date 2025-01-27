﻿using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

public class HubConnectionInitializer(IAgentHubConnection _agentHubConnection) : IHostedService
{
  public async Task StartAsync(CancellationToken cancellationToken)
  {
    await _agentHubConnection.Connect(cancellationToken);
  }

  public async Task StopAsync(CancellationToken cancellationToken)
  {
    await _agentHubConnection.DisposeAsync();
  }
}
