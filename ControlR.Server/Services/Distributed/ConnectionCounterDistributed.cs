using ControlR.Server.Services.Interfaces;

namespace ControlR.Server.Services.Distributed;

public class ConnectionCounterDistributed : IConnectionCounter
{
    public int AgentCount => throw new NotImplementedException();

    public int ViewerCount => throw new NotImplementedException();

    public void DecrementAgentCount()
    {
        throw new NotImplementedException();
    }

    public void DecrementViewerCount()
    {
        throw new NotImplementedException();
    }

    public void IncrementAgentCount()
    {
        throw new NotImplementedException();
    }

    public void IncrementViewerCount()
    {
        throw new NotImplementedException();
    }
}
