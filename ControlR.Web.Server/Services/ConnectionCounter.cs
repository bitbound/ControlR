namespace ControlR.Web.Server.Services;


public interface IConnectionCounter
{
    int AgentConnectionLocalCount { get; }
    int ViewerConnectionLocalCount { get; }

    void DecrementAgentCount();

    void DecrementViewerCount();

    Task<Result<int>> GetAgentConnectionCount();
    Task<Result<int>> GetViewerConnectionCount();

    void IncrementAgentCount();

    void IncrementViewerCount();
}

public class ConnectionCounter : IConnectionCounter
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