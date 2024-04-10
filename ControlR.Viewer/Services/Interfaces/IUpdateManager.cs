namespace ControlR.Viewer.Services.Interfaces;
internal interface IUpdateManager
{
    Task<Result<bool>> CheckForUpdate();

    Task<Result> InstallCurrentVersion();
}
