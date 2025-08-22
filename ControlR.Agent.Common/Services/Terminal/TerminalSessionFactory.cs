using ControlR.Libraries.DevicesCommon.Services.Processes;

namespace ControlR.Agent.Common.Services.Terminal;

public interface ITerminalSessionFactory
{
  Task<Result<ITerminalSession>> CreateSession(Guid terminalId, string viewerConnectionId);
}

internal class TerminalSessionFactory(
  ISystemEnvironment systemEnvironment,
  TimeProvider timeProvider,
  IHubConnection<IAgentHub> hubConnection,
  ILogger<TerminalSession> sessionLogger,
  ILogger<TerminalSessionFactory> logger) : ITerminalSessionFactory
{
  public async Task<Result<ITerminalSession>> CreateSession(Guid terminalId, string viewerConnectionId)
  {
    try
    {
      var terminalSession = new TerminalSession(
        terminalId,
        viewerConnectionId,
        timeProvider,
        systemEnvironment,
        hubConnection,
        systemEnvironment,
        sessionLogger);

      await terminalSession.Initialize();

      logger.LogInformation("Terminal session created successfully. ID: {TerminalId}, Viewer: {ViewerConnectionId}", 
        terminalId, viewerConnectionId);

      return Result.Ok<ITerminalSession>(terminalSession);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating terminal session. ID: {TerminalId}, Viewer: {ViewerConnectionId}", 
        terminalId, viewerConnectionId);
      return Result.Fail<ITerminalSession>("Failed to create terminal session.");
    }
  }
}
