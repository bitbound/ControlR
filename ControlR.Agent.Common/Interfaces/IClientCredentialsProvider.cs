using ControlR.Libraries.Ipc;

namespace ControlR.Agent.Common.Interfaces;

public interface IClientCredentialsProvider
{
  Result<IpcClientCredentials> GetClientCredentials(IIpcServer server);
}
