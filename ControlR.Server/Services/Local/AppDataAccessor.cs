using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Extensions;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Libraries.Shared.Services;
using System.Text.Json;

namespace ControlR.Server.Services.Local;

public interface IAppDataAccessor
{
    Task<Result<AlertBroadcastDto>> GetCurrentAlert();
    Task<Result> SaveCurrentAlert(AlertBroadcastDto alertDto);
    Task<Result> ClearSavedAlert();
}

public class AppDataAccessor(
    IWebHostEnvironment _hostEnv,
    IRetryer _retryer,
    ILogger<AppDataAccessor> _logger) : IAppDataAccessor
{
    private readonly string _appDataPath = Path.Combine(_hostEnv.ContentRootPath, "AppData");
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public async Task<Result> ClearSavedAlert()
    {
        try
        {
            return await _retryer.Retry(
                () =>
                {
                    var alertPath = GetAlertFilePath();
                    if (File.Exists(alertPath))
                    {
                        File.Delete(alertPath);
                    }

                    return Result.Ok().AsTaskResult();
                },
                tryCount: 5,
                TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            return Result
                .Fail(ex, "Failed to clear alert from AppData.")
                .Log(_logger);
        }
    }

    public async Task<Result<AlertBroadcastDto>> GetCurrentAlert()
    {
        try
        {
            return await _retryer.Retry(
               async () =>
               {
                   EnsureRootDirCreated();
                   var alertPath = GetAlertFilePath();
                   if (!File.Exists(alertPath))
                   {
                       return Result.Fail<AlertBroadcastDto>("No alert exists.");
                   }

                   using var fs = new FileStream(GetAlertFilePath(), FileMode.Open);
                   var alert = await JsonSerializer.DeserializeAsync<AlertBroadcastDto>(fs);
                   if (alert is not null)
                   {
                       return Result.Ok(alert);
                   }
                   return Result.Fail<AlertBroadcastDto>("Failed to deserialize alert.");
               },
               tryCount: 5,
               TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            return Result
                .Fail<AlertBroadcastDto>(ex, "Failed to get saved alert.")
                .Log(_logger);
        }
    }

    public async Task<Result> SaveCurrentAlert(AlertBroadcastDto alertDto)
    {
        try
        {
            await _retryer.Retry(
                async () =>
                {
                    EnsureRootDirCreated();
                    var alertPath = GetAlertFilePath();
                    using var fs = new FileStream(GetAlertFilePath(), FileMode.Create);
                    await JsonSerializer.SerializeAsync(fs, alertDto, _jsonOptions);
                },
                tryCount: 5,
                TimeSpan.FromSeconds(3));
            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result
                .Fail(ex, "Failed to save alert to AppData.")
                .Log(_logger);
        }
    }
    private void EnsureRootDirCreated()
    {
        Directory.CreateDirectory(_appDataPath);
    }
    private string GetAlertFilePath()
    {
        return Path.Combine(_appDataPath, "Alert.json");
    }
}
