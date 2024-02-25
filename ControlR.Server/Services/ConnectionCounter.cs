namespace ControlR.Server.Services;

public interface IConnectionCounter
{
    int AgentCount { get; }
    int ViewerCount { get; }

    void DecrementAgentCount();

    void DecrementViewerCount();

    void IncrementAgentCount();

    void IncrementViewerCount();
}

public class ConnectionCounter : IConnectionCounter
{
    private volatile int _agentCount;

    private volatile int _viewerCount;
    public int AgentCount => _agentCount;

    public int ViewerCount => _viewerCount;

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