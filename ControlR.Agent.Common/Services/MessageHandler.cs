using ControlR.Agent.Common.Models.Messages;
using ControlR.Libraries.Clients.Extensions;
using ControlR.Libraries.NativeInterop.Windows;
using ControlR.Libraries.Shared.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlR.Agent.Common.Services;

public class MessageHandler(
  IMessenger messenger,
  IServiceProvider serviceProvider,
  ILogger<MessageHandler> logger) : IHostedService
{
  private readonly ILogger<MessageHandler> _logger = logger;
  private readonly IMessenger _messenger = messenger;
  private readonly List<IDisposable> _registrations = [];
  private readonly IServiceProvider _serviceProvider = serviceProvider;

  
  public Task StartAsync(CancellationToken cancellationToken)
  {
    if (OperatingSystem.IsWindows())
    {
      _registrations.Add(
        _messenger.RegisterEvent(this, EventKinds.CtrlAltDelInvoked, HandleCtrlAltDelInvoked));
      
    }
    return Task.CompletedTask;
  }

  public Task StopAsync(CancellationToken cancellationToken)
  {
    Disposer.DisposeAll(_registrations);
    return Task.CompletedTask;
  }

  private void HandleCtrlAltDelInvoked()
  {
    try
    {
      var win32Interop = _serviceProvider.GetRequiredService<IWin32Interop>();
      win32Interop.InvokeCtrlAltDel();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while handling CtrlAltDelInvoked event.");
    }
  }
}