using ControlR.Shared.Dtos;
using ControlR.Shared.Enums;
using ControlR.Shared.Primitives;

namespace ControlR.Server.Services;

public interface IAlertStore
{
    Task<Result> ClearAlert();
    Task<Result<AlertBroadcastDto>> GetCurrentAlert();

    Task<Result> StoreAlert(AlertBroadcastDto alertDto);
}

public class AlertStore(IAppDataAccessor _appData) : IAlertStore
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

        if (alertDto.IsSticky)
        {
            return await _appData.SaveCurrentAlert(alertDto);
        }
        else
        {
            return await _appData.ClearSavedAlert();
        }
    }
}
