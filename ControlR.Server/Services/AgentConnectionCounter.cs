namespace ControlR.Server.Services;

public interface IAgentConnectionCounter
{
    int AgentCount { get; }

    void Decrement();
    void Increment();
}

public class AgentConnectionCounter : IAgentConnectionCounter
{
    private volatile int _agentCount;

    public int AgentCount => _agentCount;
    public void Increment()
    {
        Interlocked.Increment(ref _agentCount);
    }
    public void Decrement()
    {
        Interlocked.Decrement(ref _agentCount);
    }
}
