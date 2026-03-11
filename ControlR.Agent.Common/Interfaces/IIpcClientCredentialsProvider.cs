using ControlR.Libraries.Ipc;

namespace ControlR.Agent.Common.Interfaces;

public interface IIpcClientCredentialsProvider
{
  Result<IpcClientCredentials> GetClientCredentials(IIpcServer server);
}
