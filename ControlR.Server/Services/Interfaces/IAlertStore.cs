namespace ControlR.Server.Services.Interfaces;

public interface IAlertStore
{
    Task<Result> ClearAlert();
    Task<Result<AlertBroadcastDto>> GetCurrentAlert();

    Task<Result> StoreAlert(AlertBroadcastDto alertDto);
}