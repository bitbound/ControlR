namespace ControlR.Viewer.Services.Interfaces;
internal interface IStoreIntegration
{
    bool CanCheckForUpdates { get; }
    bool CanInstallUpdates { get; }

    Task<Uri> GetStorePageUri();

    Task<Uri> GetStoreProtocolUri();

    Task InstallCurrentVersion();

    Task<bool> IsProLicenseActive();

    Task<bool> IsUpdateAvailable();
}
