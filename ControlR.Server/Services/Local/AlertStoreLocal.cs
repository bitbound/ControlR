using ControlR.Libraries.Shared.Dtos;
using ControlR.Libraries.Shared.Primitives;
using ControlR.Server.Services.Interfaces;

namespace ControlR.Server.Services.Local;


public class AlertStoreLocal(IAppDataAccessor _appData) : IAlertStore
{
    private volatile AlertBroadcastDto? _currentAlert;

    public async Task<Result> ClearAlert()
    {
        _currentAlert = null;
        return await _appData.ClearSavedAlert();

    }

    public async Task<Result<AlertBroadcastDto>> GetCurrentAlert()
    {
        if (_currentAlert is not null)
        {
            return Result.Ok(_currentAlert);
        }

        return await _appData.GetCurrentAlert();
    }

    public async Task<Result> StoreAlert(AlertBroadcastDto alertDto)
    {
        _currentAlert = alertDto;

        return await _appData.SaveCurrentAlert(alertDto);
    }
}
