using ControlR.Server.Services.Interfaces;

namespace ControlR.Server.Services.InMemory;


public class ConnectionCounterLocal : IConnectionCounter
{
    private volatile int _agentCount;

    private volatile int _viewerCount;

    public Task DecrementAgentCount()
    {
        Interlocked.Decrement(ref _agentCount);
        return Task.CompletedTask;
    }

    public Task DecrementViewerCount()
    {
        Interlocked.Decrement(ref _viewerCount);
        return Task.CompletedTask;
    }

    public Task<Result<int>> GetAgentConnectionCount()
    {
        return Result.Ok(_agentCount).AsTaskResult();
    }

    public Task<Result<int>> GetViewerConnectionCount()
    {
        return Result.Ok(_viewerCount).AsTaskResult();
    }

    public Task IncrementAgentCount()
    {
        Interlocked.Increment(ref _agentCount);
        return Task.CompletedTask;
    }

    public Task IncrementViewerCount()
    {
        Interlocked.Increment(ref _viewerCount);
        return Task.CompletedTask;
    }
}