namespace ControlR.Viewer.Services.Interfaces;

internal interface IRdpLauncher
{
    Task LaunchRdp(int localPort);
}