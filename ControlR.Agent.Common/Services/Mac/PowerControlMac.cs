using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.DevicesCommon.Services.Processes;

namespace ControlR.Agent.Common.Services.Mac;

internal class PowerControlMac(IProcessManager processInvoker) : IPowerControl
{
  private readonly IProcessManager _processInvoker = processInvoker;

  public Task ChangeState(PowerStateChangeType type)
  {
    switch (type)
    {
      case PowerStateChangeType.Restart:
        {
          _ = _processInvoker.Start("shutdown", "-r now", true);
        }
        break;

      case PowerStateChangeType.Shutdown:
        {
          _ = _processInvoker.Start("shutdown", "-h now", true);
        }
        break;

      default:
        break;
    }
    return Task.CompletedTask;
  }
}