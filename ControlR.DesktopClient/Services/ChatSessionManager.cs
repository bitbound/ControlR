using System.Collections.Concurrent;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using Microsoft.Extensions.Logging;
using ControlR.DesktopClient.Common.Services;

namespace ControlR.DesktopClient.Services;

internal class ChatSessionManager(
  IProcessManager processManager,
  IIpcClientAccessor ipcClientAccessor,
  ILogger<ChatSessionManager> logger) : IChatSessionManager
{
  private readonly ConcurrentDictionary<Guid, ChatSession> _activeSessions = new();
  private readonly IProcessManager _processManager = processManager;
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly ILogger<ChatSessionManager> _logger = logger;

  public Task<Guid> CreateChatSession(Guid sessionId, int targetSystemSession, int targetProcessId, string viewerConnectionId)
  {
    var chatSession = new ChatSession
    {
      SessionId = sessionId,
      TargetSystemSession = targetSystemSession,
      TargetProcessId = targetProcessId,
      ViewerConnectionId = viewerConnectionId
    };

    _activeSessions.TryAdd(sessionId, chatSession);
    
    _logger.LogInformation(
      "Chat session created. Session ID: {SessionId}, Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}",
      sessionId,
      targetSystemSession,
      targetProcessId);

    return Task.FromResult(sessionId);
  }

  public Task AddMessage(Guid sessionId, ChatMessageIpcDto message)
  {
    if (_activeSessions.TryGetValue(sessionId, out var session))
    {
      session.Messages.Add(message);
      _logger.LogInformation(
        "Message added to chat session {SessionId} from {SenderName} ({SenderEmail})",
        sessionId,
        message.SenderName,
        message.SenderEmail);
    }
    else
    {
      _logger.LogWarning("Chat session {SessionId} not found when adding message", sessionId);
    }

    return Task.CompletedTask;
  }

  public async Task<bool> SendResponse(Guid sessionId, string message)
  {
    if (!_activeSessions.TryGetValue(sessionId, out var session))
    {
      _logger.LogWarning("Chat session {SessionId} not found when sending response", sessionId);
      return false;
    }

    if (!_ipcClientAccessor.TryGetConnection(out var connection))
    {
      _logger.LogWarning("No active IPC connection available to send chat response");
      return false;
    }

    try
    {
      // Get the current user name
      var currentUser = Environment.UserName;
      
      var response = new ChatResponseIpcDto(
        sessionId,
        message,
        currentUser,
        session.ViewerConnectionId,
        DateTimeOffset.Now);

      // Send back to Agent via IPC
      await connection!.Send(response);
      
      _logger.LogInformation(
        "Chat response sent from {Username} for session {SessionId}: {Message}",
        currentUser,
        sessionId,
        message);

      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error sending chat response for session {SessionId}", sessionId);
      return false;
    }
  }

  public Task CloseChatSession(Guid sessionId)
  {
    if (_activeSessions.TryRemove(sessionId, out var session))
    {
      session.IsActive = false;
      _logger.LogInformation("Chat session {SessionId} closed", sessionId);
    }
    else
    {
      _logger.LogWarning("Chat session {SessionId} not found when closing", sessionId);
    }

    return Task.CompletedTask;
  }

  public bool IsSessionActive(Guid sessionId)
  {
    return _activeSessions.TryGetValue(sessionId, out var session) && session.IsActive;
  }
}
