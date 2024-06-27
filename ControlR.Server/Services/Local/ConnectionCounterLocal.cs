using ControlR.Server.Services.Interfaces;

namespace ControlR.Server.Services.Local;


public class ConnectionCounterLocal : IConnectionCounter
{
    private volatile int _agentCount;

    private volatile int _streamerCount;

    private volatile int _viewerCount;

    public int AgentConnectionLocalCount => _agentCount;
    public int StreamerConnectionLocalCount => _streamerCount;
    public int ViewerConnectionLocalCount => _viewerCount;

    public void DecrementAgentCount()
    {
        Interlocked.Decrement(ref _agentCount);
    }
    public void DecrementStreamerCount()
    {
        Interlocked.Decrement(ref _streamerCount);
    }

    public void DecrementViewerCount()
    {
        Interlocked.Decrement(ref _viewerCount);
    }

    public Task<Result<int>> GetAgentConnectionCount()
    {
        return Result.Ok(_agentCount).AsTaskResult();
    }

    public Task<Result<int>> GetStreamerConnectionCount()
    {
        return Result.Ok(_streamerCount).AsTaskResult();
    }

    public Task<Result<int>> GetViewerConnectionCount()
    {
        return Result.Ok(_viewerCount).AsTaskResult();
    }

    public void IncrementAgentCount()
    {
        Interlocked.Increment(ref _agentCount);
    }
    public void IncrementStreamerCount()
    {
        Interlocked.Increment(ref _streamerCount);
    }
    public void IncrementViewerCount()
    {
        Interlocked.Increment(ref _viewerCount);
    }
}