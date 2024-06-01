namespace ControlR.Server.Services.Interfaces;

public interface IConnectionCounter
{
    int AgentCount { get; }
    int ViewerCount { get; }

    void DecrementAgentCount();

    void DecrementViewerCount();

    void IncrementAgentCount();

    void IncrementViewerCount();
}
