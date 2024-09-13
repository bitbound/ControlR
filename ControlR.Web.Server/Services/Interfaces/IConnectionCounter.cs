namespace ControlR.Web.Server.Services.Interfaces;

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
