namespace ControlR.Viewer.Services.Interfaces;
internal interface IStoreIntegration
{
    Task<Uri> GetStorePageUri();
    Task<bool> IsProLicenseActive();
}
