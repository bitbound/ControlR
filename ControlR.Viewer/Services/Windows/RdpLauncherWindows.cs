using ControlR.Viewer.Services.Interfaces;

namespace ControlR.Viewer.Services.Windows;

internal class RdpLauncherWindows : IRdpLauncher
{
    public Task<Result> LaunchRdp(int localPort)
    {
        throw new NotImplementedException();
    }
}