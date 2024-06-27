namespace ControlR.Server.Services.Interfaces;

public interface IConnectionCounter
{
    int AgentConnectionLocalCount { get; }
    int StreamerConnectionLocalCount { get; }
    int ViewerConnectionLocalCount { get; }

    void DecrementAgentCount();

    void DecrementStreamerCount();

    void DecrementViewerCount();

    Task<Result<int>> GetAgentConnectionCount();
    Task<Result<int>> GetStreamerConnectionCount();
    Task<Result<int>> GetViewerConnectionCount();

    void IncrementAgentCount();

    void IncrementStreamerCount();

    void IncrementViewerCount();
}
