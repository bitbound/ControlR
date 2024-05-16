using ControlR.Agent.Interfaces;
using ControlR.Devices.Native.Linux;

namespace ControlR.Agent.Services.Linux;

public class ElevationCheckerLinux : IElevationChecker
{
    public static IElevationChecker Instance { get; } = new ElevationCheckerLinux();

    public bool IsElevated()
    {
        return Libc.geteuid() == 0;
    }
}