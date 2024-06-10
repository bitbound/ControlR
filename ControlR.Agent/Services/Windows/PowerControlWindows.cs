using ControlR.Agent.Interfaces;
using ControlR.Libraries.DevicesCommon.Services;
using ControlR.Libraries.Shared.Enums;

namespace ControlR.Agent.Services.Windows;

internal class PowerControlWindows(IProcessManager processInvoker) : IPowerControl
{
    private readonly IProcessManager _processInvoker = processInvoker;

    public Task ChangeState(PowerStateChangeType type)
    {
        switch (type)
        {
            case PowerStateChangeType.Restart:
                {
                    _ = _processInvoker.Start("shutdown.exe", "/g /t 0 /f", true);
                }
                break;

            case PowerStateChangeType.Shutdown:
                {
                    _ = _processInvoker.Start("shutdown.exe", "/s /t 0 /f", true);
                }
                break;

            default:
                break;
        }
        return Task.CompletedTask;
    }
}