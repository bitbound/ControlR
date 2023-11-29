using ControlR.Devices.Common.Native.Linux;
using ControlR.Devices.Common.Services.Interfaces;

namespace ControlR.Devices.Common.Services.Mac;

public class ElevationCheckerMac : IElevationChecker
{
    public static IElevationChecker Instance { get; } = new ElevationCheckerMac();

    public bool IsElevated()
    {
        return Libc.geteuid() == 0;
    }
}