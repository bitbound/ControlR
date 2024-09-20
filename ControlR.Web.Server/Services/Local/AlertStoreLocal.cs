namespace ControlR.Web.Server.Services.Local;

public class AlertStoreLocal(IAppDataAccessor appData) : IAlertStore
{
  private volatile AlertBroadcastDto? _currentAlert;

  public async Task<Result> ClearAlert()
  {
    _currentAlert = null;
    return await appData.ClearSavedAlert();
  }

  public async Task<Result<AlertBroadcastDto>> GetCurrentAlert()
  {
    if (_currentAlert is not null)
    {
      return Result.Ok(_currentAlert);
    }

    return await appData.GetCurrentAlert();
  }

  public async Task<Result> StoreAlert(AlertBroadcastDto alertDto)
  {
    _currentAlert = alertDto;

    return await appData.SaveCurrentAlert(alertDto);
  }
}