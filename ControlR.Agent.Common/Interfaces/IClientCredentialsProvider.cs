using ControlR.Libraries.Ipc;

namespace ControlR.Agent.Common.Interfaces;

public interface IClientCredentialsProvider
{
  Result<ClientCredentials> GetClientCredentials(IIpcServer server);
}

public record ClientCredentials(int ProcessId, string ExecutablePath);