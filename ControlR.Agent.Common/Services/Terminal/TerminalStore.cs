using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace ControlR.Agent.Common.Services.Terminal;

public interface ITerminalStore
{
  Task<Result<TerminalSessionRequestResult>> CreateSession(Guid terminalId, string viewerConnectionId);

  bool TryRemove(Guid terminalId, [NotNullWhen(true)] out ITerminalSession? terminalSession);

  Task<Result> WriteInput(Guid terminalId, string input);
}

internal class TerminalStore(
  IServiceProvider serviceProvider,
  ILogger<TerminalStore> logger) : ITerminalStore
{
  private readonly MemoryCache _sessionCache = new(new MemoryCacheOptions());

  public async Task<Result<TerminalSessionRequestResult>> CreateSession(Guid terminalId, string viewerConnectionId)
  {
    try
    {
      var fileSystem = serviceProvider.GetRequiredService<IFileSystem>();
      var processManager = serviceProvider.GetRequiredService<IProcessManager>();
      var environment = serviceProvider.GetRequiredService<ISystemEnvironment>();
      var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
      var hubConnection = serviceProvider.GetRequiredService<IHubConnection<IAgentHub>>();
      var sessionLogger = serviceProvider.GetRequiredService<ILogger<TerminalSession>>();

      var terminalSession = new TerminalSession(
        terminalId,
        viewerConnectionId,
        timeProvider,
        environment,
        hubConnection,
        sessionLogger);

      await terminalSession.Initialize();

      var entryOptions = GetEntryOptions(terminalSession);
      _sessionCache.Set(terminalId, terminalSession, entryOptions);

      return Result.Ok(new TerminalSessionRequestResult(terminalSession.SessionKind));
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail<TerminalSessionRequestResult>("An error occurred.");
    }
  }

  public bool TryRemove(Guid terminalId, [NotNullWhen(true)] out ITerminalSession? terminalSession)
  {
    if (_sessionCache.TryGetValue(terminalId, out var cachedItem) &&
        cachedItem is TerminalSession typedItem)
    {
      terminalSession = typedItem;

      return true;
    }

    terminalSession = null;
    return false;
  }

  public async Task<Result> WriteInput(Guid terminalId, string input)
  {
    try
    {
      if (!_sessionCache.TryGetValue(terminalId, out var cachedItem))
      {
        return Result.Fail("Terminal session not found.");
      }

      if (cachedItem is not TerminalSession session)
      {
        _sessionCache.Remove(terminalId);
        return Result.Fail("Terminal session not found.");
      }

      if (session.IsDisposed)
      {
        _sessionCache.Remove(terminalId);
        return Result.Fail("Terminal session has ended.");
      }

      return await session.WriteInput(input, TimeSpan.FromSeconds(5));
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while writing terminal input.");
      return Result.Fail("An error occurred.");
    }
  }

  private static MemoryCacheEntryOptions GetEntryOptions(TerminalSession terminalSession)
  {
    var entryOptions = new MemoryCacheEntryOptions
    {
      SlidingExpiration = TimeSpan.FromMinutes(10)
    };

    var cts = new CancellationTokenSource();
    terminalSession.ProcessExited += (_, _) =>
    {
      cts.Cancel();
      cts.Dispose();
    };
    var expirationToken = new CancellationChangeToken(cts.Token);
    entryOptions.ExpirationTokens.Add(expirationToken);

    entryOptions.RegisterPostEvictionCallback((_, value, _, _) =>
    {
      if (value is TerminalSession { IsDisposed: false } session)
      {
        session.Dispose();
      }
    });

    return entryOptions;
  }
}