using ControlR.Shared.Enums;

namespace ControlR.Agent.Interfaces;
internal interface IPowerControl
{
    Task ChangeState(PowerStateChangeType type);
}
