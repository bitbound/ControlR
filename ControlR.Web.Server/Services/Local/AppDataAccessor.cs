using System.Text.Json;

namespace ControlR.Web.Server.Services.Local;

public interface IAppDataAccessor
{
  Task<Result<AlertBroadcastDto>> GetCurrentAlert();
  Task<Result> SaveCurrentAlert(AlertBroadcastDto alertDto);
  Task<Result> ClearSavedAlert();
}

public class AppDataAccessor(
  IWebHostEnvironment hostEnv,
  IRetryer retryer,
  ILogger<AppDataAccessor> logger) : IAppDataAccessor
{
  private readonly string _appDataPath = Path.Combine(hostEnv.ContentRootPath, "AppData");
  private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

  public async Task<Result> ClearSavedAlert()
  {
    try
    {
      return await retryer.Retry(
        () =>
        {
          var alertPath = GetAlertFilePath();
          if (File.Exists(alertPath))
          {
            File.Delete(alertPath);
          }

          return Result.Ok().AsTaskResult();
        },
        5,
        TimeSpan.FromSeconds(3));
    }
    catch (Exception ex)
    {
      return Result
        .Fail(ex, "Failed to clear alert from AppData.")
        .Log(logger);
    }
  }

  public async Task<Result<AlertBroadcastDto>> GetCurrentAlert()
  {
    try
    {
      return await retryer.Retry(
        async () =>
        {
          EnsureRootDirCreated();
          var alertPath = GetAlertFilePath();
          if (!File.Exists(alertPath))
          {
            return Result.Fail<AlertBroadcastDto>("No alert exists.");
          }

          await using var fs = new FileStream(GetAlertFilePath(), FileMode.Open);
          var alert = await JsonSerializer.DeserializeAsync<AlertBroadcastDto>(fs);
          if (alert is not null)
          {
            return Result.Ok(alert);
          }

          return Result.Fail<AlertBroadcastDto>("Failed to deserialize alert.");
        },
        5,
        TimeSpan.FromSeconds(3));
    }
    catch (Exception ex)
    {
      return Result
        .Fail<AlertBroadcastDto>(ex, "Failed to get saved alert.")
        .Log(logger);
    }
  }

  public async Task<Result> SaveCurrentAlert(AlertBroadcastDto alertDto)
  {
    try
    {
      await retryer.Retry(
        async () =>
        {
          EnsureRootDirCreated();
          var alertPath = GetAlertFilePath();
          await using var fs = new FileStream(GetAlertFilePath(), FileMode.Create);
          await JsonSerializer.SerializeAsync(fs, alertDto, _jsonOptions);
        },
        5,
        TimeSpan.FromSeconds(3));
      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result
        .Fail(ex, "Failed to save alert to AppData.")
        .Log(logger);
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