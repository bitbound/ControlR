using System.Collections.Concurrent;
using System.Drawing;
using Avalonia.Controls;
using Avalonia.Threading;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.DesktopClient.Common.Services;
using ControlR.DesktopClient.Models;
using ControlR.DesktopClient.ViewModels;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

internal class ChatSessionManager(
  IServiceProvider serviceProvider,
  IIpcClientAccessor ipcClientAccessor,
  IToaster toaster,
  ILogger<ChatSessionManager> logger) : IChatSessionManager
{
  private readonly ConcurrentDictionary<Guid, ChatSession> _activeSessions = new();
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly ILogger<ChatSessionManager> _logger = logger;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly IToaster _toaster = toaster;

  public Task AddMessage(Guid sessionId, ChatMessageIpcDto message)
  {
    Dispatcher.UIThread.Invoke(async () =>
    {
      var session = _activeSessions.AddOrUpdate(
        sessionId,
        _ =>
        {
          var chatWindow = _serviceProvider.GetRequiredService<ChatWindow>();
          chatWindow.DataContext ??= _serviceProvider.GetRequiredService<IChatWindowViewModel>();

          var newSession = new ChatSession
          {
            ChatWindow = chatWindow,
            SessionId = sessionId,
            TargetSystemSession = message.TargetSystemSession,
            TargetProcessId = message.TargetProcessId,
            ViewerConnectionId = message.ViewerConnectionId,
            Messages = [message],
            CreatedAt = DateTimeOffset.Now,
          };

          chatWindow.ViewModel.Session = newSession;

          _logger.LogInformation(
            "New chat session created. Session ID: {SessionId}, Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}",
            sessionId,
            message.TargetSystemSession,
            message.TargetProcessId);

          return newSession;
        },
        (sessionId, existingSession) =>
        {
          if (existingSession.ChatWindow?.PlatformImpl is not { })
          {
            existingSession.ChatWindow = _serviceProvider.GetRequiredService<ChatWindow>();
            existingSession.ChatWindow.DataContext = _serviceProvider.GetRequiredService<IChatWindowViewModel>();
            existingSession.ChatWindow.ViewModel.Session = existingSession;
          }

          existingSession.ViewerConnectionId = message.ViewerConnectionId;
          existingSession.Messages.Add(message);
          return existingSession;
        });

      Guard.IsNotNull(session.ChatWindow);

      if (!session.ChatWindow.IsVisible)
      {
        session.ChatWindow.Show();
        session.ChatWindow.Activate();
      }

      if (session.ChatWindow.WindowState == WindowState.Minimized)
      {
        await _toaster.ShowToast(
          title: Localization.NewChatMessageToastTitle,
          message: string.Format(Localization.NewChatMessageToastMessage, message.SenderName),
          toastIcon: ToastIcon.Info,
          onClick: () =>
          {
            session.ChatWindow.WindowState = WindowState.Normal;
            session.ChatWindow.Show();
          });
      }
    });

    return Task.CompletedTask;
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
}
