namespace ControlR.Web.Server.Services;


public interface IConnectionCounter
{
    int AgentConnectionCount { get; }
    int ViewerConnectionCount { get; }

    void DecrementAgentCount();

    void DecrementViewerCount();

    void IncrementAgentCount();

    void IncrementViewerCount();
}

public class ConnectionCounter : IConnectionCounter
{
    private volatile int _agentCount;

    private volatile int _viewerCount;

    public int AgentConnectionCount => _agentCount;
    public int ViewerConnectionCount => _viewerCount;

    public void DecrementAgentCount()
    {
        Interlocked.Decrement(ref _agentCount);
    }

    public void DecrementViewerCount()
    {
        Interlocked.Decrement(ref _viewerCount);
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