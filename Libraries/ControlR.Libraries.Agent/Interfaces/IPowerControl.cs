using ControlR.Libraries.Shared.Enums;

namespace ControlR.Libraries.Agent.Interfaces;
internal interface IPowerControl
{
  Task ChangeState(PowerStateChangeType type);
}
