using Microsoft.AspNetCore.SignalR.Client;

namespace ControlR.Agent.LoadTester;

public class TestAgentRetryPolicy : IRetryPolicy
{
  public TimeSpan? NextRetryDelay(RetryContext retryContext)
  {
    return TimeSpan.FromSeconds(10);
  }
}
