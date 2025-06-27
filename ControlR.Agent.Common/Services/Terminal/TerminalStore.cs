using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace ControlR.Agent.Common.Services.Terminal;

public interface ITerminalStore
{
  Task<Result> CreateSession(Guid terminalId, string viewerConnectionId);
  Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto requestDto);
  bool TryRemove(Guid terminalId, [NotNullWhen(true)] out ITerminalSession? terminalSession);

  Task<Result> WriteInput(Guid terminalId, string input, CancellationToken cancellationToken);
}

internal class TerminalStore(
  IServiceProvider serviceProvider,
  ILogger<TerminalStore> logger) : ITerminalStore
{
  private readonly MemoryCache _sessionCache = new(new MemoryCacheOptions());

  public async Task<Result> CreateSession(Guid terminalId, string viewerConnectionId)
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

      return Result.Ok();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail("An error occurred.");
    }
  }

  public Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto requestDto)
  {
    if (!TryGetTerminalSession(requestDto.TerminalId, out var session))
    {
      logger.LogWarning("No terminal session found for ID: {TerminalId}", requestDto.TerminalId);
      return Result.Fail<PwshCompletionsResponseDto>("Terminal session not found.").AsTaskResult();
    }

    var completions = session.GetCompletions(requestDto);
    return Result.Ok(completions).AsTaskResult();
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

  public async Task<Result> WriteInput(Guid terminalId, string input, CancellationToken cancellationToken)
  {
    try
    {
      if (!TryGetTerminalSession(terminalId, out var session))
      {
        logger.LogWarning("No terminal session found for ID: {TerminalId}", terminalId);
        return Result.Fail("Terminal session not found.");
      }

      return await session.WriteInput(input, cancellationToken);
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

  private bool TryGetTerminalSession(Guid terminalId, [NotNullWhen(true)] out ITerminalSession? terminalSession)
  {
    terminalSession = null;
    if (!_sessionCache.TryGetValue(terminalId, out var cachedItem))
    {
      return false;
    }

    if (cachedItem is not TerminalSession session)
    {
      _sessionCache.Remove(terminalId);
      return false;
    }

    if (session.IsDisposed)
    {
      _sessionCache.Remove(terminalId);
      return false;
    }

    terminalSession = session;
    return true;
  }
}