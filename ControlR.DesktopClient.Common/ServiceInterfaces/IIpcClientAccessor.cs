using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Ipc;

namespace ControlR.DesktopClient.Common.ServiceInterfaces;

public interface IIpcClientAccessor
{
  bool TryGetClient([NotNullWhen(true)] out IIpcClient? connection);
}
