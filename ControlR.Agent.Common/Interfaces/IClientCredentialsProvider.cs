using ControlR.Libraries.Ipc;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.Common.Interfaces;

public interface IClientCredentialsProvider
{
  Result<ClientCredentials> GetClientCredentials(IIpcServer server);
}

public record ClientCredentials(int ProcessId, string ExecutablePath);