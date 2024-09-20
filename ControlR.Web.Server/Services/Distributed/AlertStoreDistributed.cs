using MessagePack;
using Microsoft.Extensions.Caching.Distributed;

namespace ControlR.Web.Server.Services.Distributed;

public class AlertStoreDistributed(
  IDistributedCache cache,
  IHostApplicationLifetime appLifetime,
  ILogger<AlertStoreDistributed> logger) : IAlertStore
{
  public async Task<Result> ClearAlert()
  {
    try
    {
      await cache.RemoveAsync(DistributedCacheKeys.AlertStore, appLifetime.ApplicationStopping);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result.Fail(ex, "Error while clearing alert.").Log(logger);
    }
  }

  public async Task<Result<AlertBroadcastDto>> GetCurrentAlert()
  {
    try
    {
      var value = await cache.GetAsync(DistributedCacheKeys.AlertStore, appLifetime.ApplicationStopping);
      if (value is null)
      {
        return Result.Fail<AlertBroadcastDto>("No current alert.");
      }

      var dto = MessagePackSerializer.Deserialize<AlertBroadcastDto>(value);
      return Result.Ok(dto);
    }
    catch (Exception ex)
    {
      return Result.Fail<AlertBroadcastDto>(ex, "Error while getting current alert.").Log(logger);
    }
  }

  public async Task<Result> StoreAlert(AlertBroadcastDto alertDto)
  {
    try
    {
      var value = MessagePackSerializer.Serialize(alertDto);
      await cache.SetAsync(DistributedCacheKeys.AlertStore, value);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result.Fail(ex, "Error while storing alert.").Log(logger);
    }
  }
}