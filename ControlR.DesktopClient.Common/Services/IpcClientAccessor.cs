using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Ipc;

namespace ControlR.DesktopClient.Common.Services;

public interface IIpcClientAccessor
{
  void SetConnection(IIpcClient? connection);
  bool TryGetConnection([NotNullWhen(true)] out IIpcClient? connection);
}

public class IpcClientAccessor : IIpcClientAccessor
{
  private IIpcClient? _currentConnection;
  
  public void SetConnection(IIpcClient? connection)
  {
    _currentConnection = connection;
  }

  public bool TryGetConnection([NotNullWhen(true)] out IIpcClient? connection)
  {
    connection = _currentConnection;
    return connection is not null;
  }
}
