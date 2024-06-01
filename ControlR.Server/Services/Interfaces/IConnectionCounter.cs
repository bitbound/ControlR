namespace ControlR.Server.Services.Interfaces;

public interface IConnectionCounter
{
    Task<Result<int>> GetAgentConnectionCount();
    Task<Result<int>> GetViewerConnectionCount();

    Task DecrementAgentCount();

    Task DecrementViewerCount();

    Task IncrementAgentCount();

    Task IncrementViewerCount();
}
