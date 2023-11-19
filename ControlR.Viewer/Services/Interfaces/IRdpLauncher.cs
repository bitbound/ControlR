namespace ControlR.Viewer.Services.Interfaces;

public interface IRdpLauncher
{
    Task<Result> LaunchRdp(int localPort);
}