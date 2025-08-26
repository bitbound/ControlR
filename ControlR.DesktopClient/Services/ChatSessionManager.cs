using System.Collections.Concurrent;
using ControlR.DesktopClient.Common.Models;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.DevicesCommon.Services.Processes;
using Microsoft.Extensions.Logging;
using ControlR.DesktopClient.Common.Services;

namespace ControlR.DesktopClient.Services;

public interface IChatSessionManager
{
  Task AddMessage(Guid sessionId, ChatMessageIpcDto message);
  Task<bool> SendResponse(Guid sessionId, string message);
  Task CloseChatSession(Guid sessionId);
  bool IsSessionActive(Guid sessionId);
}

internal class ChatSessionManager(
  IIpcClientAccessor ipcClientAccessor,
  ILogger<ChatSessionManager> logger) : IChatSessionManager
{
  private readonly ConcurrentDictionary<Guid, ChatSession> _activeSessions = new();
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly ILogger<ChatSessionManager> _logger = logger;

  public Task AddMessage(Guid sessionId, ChatMessageIpcDto message)
  {
    var session = _activeSessions.AddOrUpdate(
      sessionId,
      _ =>
      {
        var newSession = new ChatSession
        {
          SessionId = sessionId,
          TargetSystemSession = message.TargetSystemSession,
          TargetProcessId = message.TargetProcessId,
          ViewerConnectionId = message.ViewerConnectionId,
          Messages = [message],
          CreatedAt = DateTimeOffset.Now,
        };

        _logger.LogInformation(
          "New chat session created. Session ID: {SessionId}, Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}",
          sessionId,
          message.TargetSystemSession,
          message.TargetProcessId);

        return newSession;
      },
      (sessionId, existingSession) =>
      {
        existingSession.ViewerConnectionId = message.ViewerConnectionId;
        existingSession.Messages.Add(message);
        return existingSession;
      });

    // TODO: Show chat UI notification or update existing chat window
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
      await connection.Send(response);

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
    return _activeSessions.TryGetValue(sessionId, out _);
  }
}
