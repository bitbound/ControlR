using ControlR.Agent.Interfaces;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.Agent.Services.Mac;

internal class PowerControlMac(IProcessManager processInvoker) : IPowerControl
{
    private readonly IProcessManager _processInvoker = processInvoker;

    public Task ChangeState(PowerStateChangeType type)
    {
        switch (type)
        {
            case PowerStateChangeType.Restart:
                {
                    _ = _processInvoker.Start("shutdown", "-r 0", true);
                }
                break;

            case PowerStateChangeType.Shutdown:
                {
                    _ = _processInvoker.Start("shutdown", "-P 0", true);
                }
                break;

            default:
                break;
        }
        return Task.CompletedTask;
    }
}