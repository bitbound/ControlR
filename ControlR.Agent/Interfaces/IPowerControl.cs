using ControlR.Libraries.Shared.Enums;

namespace ControlR.Agent.Interfaces;
internal interface IPowerControl
{
    Task ChangeState(PowerStateChangeType type);
}
