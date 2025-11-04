using System.Runtime.Versioning;
using ControlR.Agent.Common.Interfaces;
using ControlR.Libraries.Ipc;
using ControlR.Libraries.NativeInterop.Unix.Linux;
using ControlR.Libraries.NativeInterop.Unix.MacOs;
using ControlR.Libraries.Shared.Primitives;

namespace ControlR.Agent.Common.Services.Mac;

[SupportedOSPlatform("macos")]
internal class ClientCredentialsProviderMac : IClientCredentialsProvider
{
  public Result<Interfaces.ClientCredentials> GetClientCredentials(IIpcServer server)
  {
    if (!server.TryGetServerHandle(out var handle))
    {
      return Result.Fail<Interfaces.ClientCredentials>("Failed to get server handle from IPC connection.");
    }

    var nativeResult = UnixSocketClientInfoMac.GetClientCredentials(handle);
    if (!nativeResult.IsSuccess)
    {
      return Result.Fail<Interfaces.ClientCredentials>(nativeResult.Reason);
    }

    // Convert native ClientCredentials to common ClientCredentials
    return Result.Ok(new Interfaces.ClientCredentials(
      nativeResult.Value.ProcessId,
      nativeResult.Value.ExecutablePath));
  }
}
