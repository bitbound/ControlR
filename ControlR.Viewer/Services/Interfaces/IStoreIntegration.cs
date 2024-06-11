namespace ControlR.Viewer.Services.Interfaces;
internal interface IStoreIntegration
{
    Task<Uri> GetStorePageUri();

    Task<Uri> GetStoreProtocolUri();

    Task<Result> InstallCurrentVersion();

    Task<Result<bool>> IsProLicenseActive();

    Task<Result<bool>> IsUpdateAvailable();
}
