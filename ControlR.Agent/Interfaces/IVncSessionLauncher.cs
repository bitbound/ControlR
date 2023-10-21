using ControlR.Shared;
using System.Diagnostics;

namespace ControlR.Agent.Interfaces;

internal interface IVncSessionLauncher
{
    Task<Result<Process>> CreateSession(
        Guid sessionId,
        string password,
        Func<double, Task>? onDownloadProgress = null);
}