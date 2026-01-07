using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Shared.Dtos.HubDtos.PwshCommandCompletions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace ControlR.Agent.Common.Services.Terminal;

public interface ITerminalStore
{
  Task<Result> CreateSession(Guid terminalId, string viewerConnectionId);
  Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto requestDto);
  bool TryRemove(Guid terminalId, [NotNullWhen(true)] out ITerminalSession? terminalSession);

  Task<Result> WriteInput(Guid terminalId, string input, string viewerConnectionId, CancellationToken cancellationToken);
}

internal class TerminalStore(
  ITerminalSessionFactory sessionFactory,
  ILogger<TerminalStore> logger) : ITerminalStore
{
  private readonly MemoryCache _sessionCache = new(new MemoryCacheOptions());

  public async Task<Result> CreateSession(Guid terminalId, string viewerConnectionId)
  {
    try
    {
      var sessionResult = await GetOrCreateSession(terminalId, viewerConnectionId);
      if (!sessionResult.IsSuccess)
      {
        return Result.Fail(sessionResult.Reason);
      }
      return Result.Ok();
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating terminal session.");
      return Result.Fail("An error occurred.");
    }
  }

  public async Task<Result<PwshCompletionsResponseDto>> GetPwshCompletions(PwshCompletionsRequestDto requestDto)
  {
    var sessionResult = await GetOrCreateSession(requestDto.TerminalId, requestDto.ViewerConnectionId);
    if (!sessionResult.IsSuccess)
    {
      logger.LogWarning("Failed to get or create terminal session for ID: {TerminalId}", requestDto.TerminalId);
      return Result.Fail<PwshCompletionsResponseDto>("Failed to get or create terminal session.");
    }

    var completions = sessionResult.Value.GetCompletions(requestDto);
    return Result.Ok(completions);
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

  public async Task<Result> WriteInput(Guid terminalId, string input, string viewerConnectionId, CancellationToken cancellationToken)
  {
    try
    {
      var sessionResult = await GetOrCreateSession(terminalId, viewerConnectionId);
      if (!sessionResult.IsSuccess)
      {
        logger.LogWarning("Failed to get or create terminal session for ID: {TerminalId}", terminalId);
        return Result.Fail("Failed to get or create terminal session.");
      }

      return await sessionResult.Value.WriteInput(input, cancellationToken);
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

  private async Task<Result<ITerminalSession>> GetOrCreateSession(
    Guid terminalId,
    string viewerConnectionId)
  {
    // First try to get existing session
    if (_sessionCache.TryGetValue(terminalId, out var cachedItem) &&
        cachedItem is TerminalSession existingSession &&
        !existingSession.IsDisposed)
    {
      return Result.Ok<ITerminalSession>(existingSession);
    }

    // Clean up any invalid cached item
    if (cachedItem is not null)
    {
      _sessionCache.Remove(terminalId);
    }

    // Create new session using factory
    try
    {
      var sessionResult = await sessionFactory.CreateSession(terminalId, viewerConnectionId);
      if (!sessionResult.IsSuccess)
      {
        return Result.Fail<ITerminalSession>(sessionResult.Reason);
      }

      var terminalSession = (TerminalSession)sessionResult.Value;
      var entryOptions = GetEntryOptions(terminalSession);
      _sessionCache.Set(terminalId, terminalSession, entryOptions);

      return Result.Ok<ITerminalSession>(terminalSession);
    }
    catch (Exception ex)
    {
      logger.LogError(ex, "Error while creating terminal session for ID: {TerminalId}", terminalId);
      return Result.Fail<ITerminalSession>("Failed to create terminal session.");
    }
  }
}