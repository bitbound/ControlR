using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Server.Services.Interfaces;
using MessagePack;
using Microsoft.Extensions.Caching.Distributed;

namespace ControlR.Server.Services.Distributed;

public class AlertStoreDistributed(
    IDistributedCache _cache,
    IHostApplicationLifetime _appLifetime,
    ILogger<AlertStoreDistributed> _logger) : IAlertStore
{
    public async Task<Result> ClearAlert()
    {
        try
        {
            await _cache.RemoveAsync(DistributedCacheKeys.AlertStore, _appLifetime.ApplicationStopping);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail(ex, "Error while clearing alert.").Log(_logger);
        }
    }

    public async Task<Result<AlertBroadcastDto>> GetCurrentAlert()
    {
        try
        {
            var value = await _cache.GetAsync(DistributedCacheKeys.AlertStore, _appLifetime.ApplicationStopping);
            if (value is null)
            {
                return Result.Fail<AlertBroadcastDto>("No current alert.");
            }
            var dto = MessagePackSerializer.Deserialize<AlertBroadcastDto>(value);
            return Result.Ok(dto);
        }
        catch (Exception ex)
        {
            return Result.Fail<AlertBroadcastDto>(ex, "Error while getting current alert.").Log(_logger);
        }
    }

    public async Task<Result> StoreAlert(AlertBroadcastDto alertDto)
    {
        try
        {
            var value = MessagePackSerializer.Serialize(alertDto);
            await _cache.SetAsync(DistributedCacheKeys.AlertStore, value);
            return Result.Ok();

        }
        catch (Exception ex)
        {
            return Result.Fail(ex, "Error while storing alert.").Log(_logger);
        }
    }
}
