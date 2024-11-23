using ControlR.Libraries.Shared.Enums;

namespace ControlR.Agent.Common.Interfaces;
internal interface IPowerControl
{
  Task ChangeState(PowerStateChangeType type);
}
