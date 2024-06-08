using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Server.Services.Interfaces;

namespace ControlR.Server.Services.Local;


public class ConnectionCounterLocal : IConnectionCounter
{
    private volatile int _agentCount;

    private volatile int _viewerCount;

    public int AgentConnectionLocalCount => _agentCount;
    public int ViewerConnectionLocalCount => _viewerCount;

    public void DecrementAgentCount()
    {
        Interlocked.Decrement(ref _agentCount);
    }

    public void DecrementViewerCount()
    {
        Interlocked.Decrement(ref _viewerCount);
    }

    public Task<Result<int>> GetAgentConnectionCount()
    {
        return Result.Ok(_agentCount).AsTaskResult();
    }

    public Task<Result<int>> GetViewerConnectionCount()
    {
        return Result.Ok(_viewerCount).AsTaskResult();
    }

    public void IncrementAgentCount()
    {
        Interlocked.Increment(ref _agentCount);
    }

    public void IncrementViewerCount()
    {
        Interlocked.Increment(ref _viewerCount);
    }
}