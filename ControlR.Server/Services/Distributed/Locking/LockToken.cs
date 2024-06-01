using StackExchange.Redis;

namespace ControlR.Server.Services.Distributed.Locking;

public sealed class LockToken : IAsyncDisposable
{
    private readonly Func<Task>? _disposalFunc;
    private readonly ILogger? _logger;

    private LockToken(RedisKey _lockKey)
    {
        LockKey = _lockKey;
    }

    private LockToken(
        RedisKey lockKey,
        RedisValue lockValue,
        Func<Task> disposalFunc,
        ILogger logger)
    {
        LockKey = lockKey;
        LockValue = lockValue;
        _disposalFunc = disposalFunc;
        _logger = logger;
    }

    public RedisKey LockKey { get; }
    public bool LockAcquired { get; init; }
    public RedisValue LockValue { get; }
    public static LockToken Failed(RedisKey lockKey)
    {
        return new LockToken(lockKey)
        {
            LockAcquired = false
        };
    }

    public static LockToken Succeeded(
        RedisKey lockKey, 
        RedisValue lockValue, 
        ILogger logger, 
        Func<Task> disposalFunc)
    {
        return new LockToken(lockKey, lockValue, disposalFunc, logger)
        {
            LockAcquired = true
        };
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_disposalFunc is not null)
            {
                await _disposalFunc.Invoke();
            }
        }
        catch (Exception ex)
        { 
            if (_logger is not null)
            {
                _logger.LogError(ex, "Error while disposing of distributed lock.");
            }
        }
    }
}
