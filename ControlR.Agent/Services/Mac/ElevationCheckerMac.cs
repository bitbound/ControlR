using ControlR.Agent.Interfaces;
using ControlR.Devices.Native.Linux;

namespace ControlR.Agent.Services.Mac;

public class ElevationCheckerMac : IElevationChecker
{
    public static IElevationChecker Instance { get; } = new ElevationCheckerMac();

    public bool IsElevated()
    {
        return Libc.geteuid() == 0;
    }
}