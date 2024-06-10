using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System.Diagnostics.CodeAnalysis;

namespace ControlR.Agent.Services;

public interface ITerminalStore
{
    Task<Result<TerminalSessionRequestResult>> CreateSession(Guid terminalId, string viewerConnectionId);

    bool TryRemove(Guid terminalId, [NotNullWhen(true)] out ITerminalSession? terminalSession);

    Task<Result> WriteInput(Guid terminalId, string input);
}

internal class TerminalStore(
    IServiceProvider _serviceProvider,
    ILogger<TerminalStore> _logger) : ITerminalStore
{
    private readonly MemoryCache _sessionCache = new(new MemoryCacheOptions());

    public async Task<Result<TerminalSessionRequestResult>> CreateSession(Guid terminalId, string viewerConnectionId)
    {
        try
        {
            var fileSystem = _serviceProvider.GetRequiredService<IFileSystem>();
            var processManager = _serviceProvider.GetRequiredService<IProcessManager>();
            var environment = _serviceProvider.GetRequiredService<IEnvironmentHelper>();
            var systemTime = _serviceProvider.GetRequiredService<ISystemTime>();
            var hubConnection = _serviceProvider.GetRequiredService<IAgentHubConnection>();
            var logger = _serviceProvider.GetRequiredService<ILogger<TerminalSession>>();

            var terminalSession = new TerminalSession(
                terminalId,
                viewerConnectionId,
                fileSystem,
                processManager,
                environment,
                systemTime,
                hubConnection,
                logger);

            await terminalSession.Initialize();

            var entryOptions = GetEntryOptions(terminalSession);
            _sessionCache.Set(terminalId, terminalSession, entryOptions);

            return Result.Ok(new TerminalSessionRequestResult(terminalSession.SessionKind));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating terminal session.");
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
            if (_sessionCache.TryGetValue(terminalId, out var cacheItem) &&
                cacheItem is TerminalSession session)
            {
                await session.WriteInput(input, TimeSpan.FromSeconds(5));
                return Result.Ok();
            }
            return Result.Fail("Failed to write terminal input.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while writing terminal input.");
            return Result.Fail("An error occurred.");
        }
    }

    private static MemoryCacheEntryOptions GetEntryOptions(TerminalSession terminalSession)
    {
        var entryOptions = new MemoryCacheEntryOptions()
        {
            SlidingExpiration = TimeSpan.FromMinutes(10),
        };

        var cts = new CancellationTokenSource();
        terminalSession.ProcessExited += (s, e) =>
        {
            cts.Cancel();
            cts.Dispose();
        };
        var expirationToken = new CancellationChangeToken(cts.Token);
        entryOptions.ExpirationTokens.Add(expirationToken);

        entryOptions.RegisterPostEvictionCallback((object key, object? value, EvictionReason reason, object? state) =>
        {
            if (value is TerminalSession terminalSession &&
                !terminalSession.IsDisposed)
            {
                terminalSession.Dispose();
            }
        });

        return entryOptions;
    }
}