using ControlR.Devices.Common.Native.Linux;
using ControlR.Devices.Common.Services.Interfaces;

namespace ControlR.Devices.Common.Services.Linux;

public class ElevationCheckerLinux : IElevationChecker
{
    public static IElevationChecker Instance { get; } = new ElevationCheckerLinux();

    public bool IsElevated()
    {
        return Libc.geteuid() == 0;
    }
}