using ControlR.Agent.Models;
using ControlR.Shared.Primitives;

namespace ControlR.Agent.Interfaces;

internal interface IVncSessionLauncher
{
    Task<Result<VncSession>> CreateSession(Guid sessionId, string password);
}