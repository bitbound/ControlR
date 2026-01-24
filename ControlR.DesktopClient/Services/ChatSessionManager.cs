using System.Collections.Concurrent;
using Avalonia.Controls;
using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Models;
using ControlR.DesktopClient.ViewModels;
using ControlR.Libraries.Shared.Dtos.IpcDtos;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Logging;

namespace ControlR.DesktopClient.Services;

internal class ChatSessionManager(
  IServiceProvider serviceProvider,
  IIpcClientAccessor ipcClientAccessor,
  ISystemEnvironment systemEnvironment,
  IToaster toaster,
  ILogger<ChatSessionManager> logger) : IChatSessionManager
{
  private readonly IIpcClientAccessor _ipcClientAccessor = ipcClientAccessor;
  private readonly ILogger<ChatSessionManager> _logger = logger;
  private readonly IServiceProvider _serviceProvider = serviceProvider;
  private readonly ConcurrentDictionary<Guid, ChatSession> _sessions = new();
  private readonly ISystemEnvironment _systemEnvironment = systemEnvironment;
  private readonly IToaster _toaster = toaster;
  private readonly ConcurrentDictionary<Guid, ChatWindow> _windows = new();

  public Task AddMessage(Guid sessionId, ChatMessageIpcDto message)
  {
    Dispatcher.UIThread.Invoke(async () =>
    {
      var (session, window) = GetOrCreateSession(sessionId, message);

      session.Messages.Add(new ChatMessageViewModel(message, true));

      if (!window.IsVisible)
      {
        window.Show();
        window.Activate();
      }

      if (window.WindowState == WindowState.Minimized)
      {
        await _toaster.ShowToast(
          title: Localization.NewChatMessageToastTitle,
          message: string.Format(Localization.NewChatMessageToastMessage, message.SenderName),
          toastIcon: ToastIcon.Info,
          onClick: () =>
          {
            window.WindowState = WindowState.Normal;
            window.Show();
          });
      }
    });

    return Task.CompletedTask;
  }
  public Task CloseChatSession(Guid sessionId, bool notifyUser)
  {
    Dispatcher.UIThread.Invoke(async () =>
    {
      if (_sessions.TryRemove(sessionId, out _))
      {
        _logger.LogInformation("Chat session {SessionId} closed", sessionId);

        // Close and remove the chat window if it exists
        if (_windows.TryRemove(sessionId, out var window))
        {
          window.Close();
        }

        if (notifyUser)
        {
          await _toaster.ShowToast(
            title: Localization.ChatSessionClosedToastTitle,
            message: Localization.ChatSessionClosedToastMessage,
            toastIcon: ToastIcon.Info);
        }
      }
      else
      {
        _logger.LogWarning("Chat session {SessionId} not found when closing", sessionId);
      }
    });

    return Task.CompletedTask;
  }
  public bool IsSessionActive(Guid sessionId)
  {
    return _sessions.ContainsKey(sessionId);
  }
  public async Task<bool> SendResponse(Guid sessionId, string message)
  {
    if (!_sessions.TryGetValue(sessionId, out var session))
    {
      _logger.LogWarning("Chat session {SessionId} not found when sending response", sessionId);
      return false;
    }

    if (!_ipcClientAccessor.TryGetClient(out var connection))
    {
      _logger.LogWarning("No active IPC connection available to send chat response");
      return false;
    }

    try
    {
      // Get the current username
      var currentUser = Environment.UserName;
      if (OperatingSystem.IsWindows())
      {
        // On Windows, we're running as SYSTEM to allow input to UAC/WinLogon.
        var win32Interop = _serviceProvider.GetRequiredService<IWin32Interop>();
        if (win32Interop.GetUsernameFromSessionId((uint)session.TargetSystemSession) is { Length: > 0 } sessionUsername)
        {
          currentUser = sessionUsername;
        }
      }

      var response = new ChatResponseIpcDto(
        sessionId,
        _systemEnvironment.ProcessId,
        message,
        currentUser,
        session.ViewerConnectionId,
        DateTimeOffset.Now);

      // Send back to Agent via IPC
      var success = await connection.Server.SendChatResponse(response);
      if (!success)
      {
        _logger.LogWarning("IPC client reported failure sending chat response for session {SessionId}", sessionId);
        return false;
      }

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

  private ChatWindow CreateWindowForSession(ChatSession session)
  {
    var window = _serviceProvider.GetRequiredService<ChatWindow>();
    var viewModel = window.ViewModel;
    viewModel.Session = session;

    return window;
  }
  private (ChatSession session, ChatWindow window) GetOrCreateSession(Guid sessionId, ChatMessageIpcDto message)
  {
    // Get or create the session data
    var session = _sessions.GetOrAdd(
      sessionId,
      _ =>
      {
        var newSession = new ChatSession
        {
          SessionId = sessionId,
          TargetSystemSession = message.TargetSystemSession,
          TargetProcessId = message.TargetProcessId,
          ViewerConnectionId = message.ViewerConnectionId,
          ViewerName = message.SenderName,
          CreatedAt = DateTimeOffset.Now,
        };

        _logger.LogInformation(
          "New chat session created. Session ID: {SessionId}, Target System Session: {TargetSystemSession}, Process ID: {TargetProcessId}",
          sessionId,
          message.TargetSystemSession,
          message.TargetProcessId);

        return newSession;
      });

    // Update viewer connection ID if it changed
    if (session.ViewerConnectionId != message.ViewerConnectionId)
    {
      session.ViewerConnectionId = message.ViewerConnectionId;
    }

    // Get or create the window
    var window = _windows.GetOrAdd(
      sessionId,
      _ => CreateWindowForSession(session));

    // If window was closed/disposed, recreate it
    if (window.PlatformImpl is null)
    {
      window = CreateWindowForSession(session);
      _windows[sessionId] = window;
    }

    return (session, window);
  }
}
