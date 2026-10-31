using ControlR.DesktopClient.Common;
using ControlR.DesktopClient.Common.Options;
using ControlR.DesktopClient.Common.ServiceInterfaces;
using ControlR.DesktopClient.Common.ServiceInterfaces.Toaster;
using ControlR.Libraries.Api.Contracts.Dtos.IpcDtos;
using ControlR.Libraries.Avalonia.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ControlR.DesktopClient.Mac.Services;

public class MacRemoteControlHostBuilderFactory(
  IOptionsMonitor<DesktopClientOptions> desktopClientOptions,
  IUserInteractionService userInteractionService,
  IRemoteControlDtoSender remoteControlDtoSender,
  IToaster toaster,
  IUiDispatcher dispatcher) : IRemoteControlHostBuilderFactory
{
  private readonly IOptionsMonitor<DesktopClientOptions> _desktopClientOptions = desktopClientOptions;
  private readonly IUiDispatcher _dispatcher = dispatcher;
  private readonly IRemoteControlDtoSender _remoteControlDtoSender = remoteControlDtoSender;
  private readonly IToaster _toaster = toaster;
  private readonly IUserInteractionService _userInteractionService = userInteractionService;

  public HostApplicationBuilder CreateHostBuilder(RemoteControlRequestIpcDto requestDto)
  {
    var builder = Host.CreateApplicationBuilder();

    builder.AddCommonRemoteControlServices(
      new RemoteControlHostDependencies
      {
        Toaster = _toaster,
        UserInteraction = _userInteractionService,
        DtoSender = _remoteControlDtoSender,
        Dispatcher = _dispatcher
      },
      options =>
      {
        options.SessionId = requestDto.SessionId;
        options.NotifyUser = requestDto.NotifyUserOnSessionStart;
        options.RequireConsent = requestDto.RequireConsent;
        options.ViewerName = requestDto.ViewerName;
        options.ViewerConnectionId = requestDto.ViewerConnectionId;
        options.ViewerSupportsRemoteControlDtoHub = requestDto.ViewerSupportsRemoteControlDtoHub;
        options.WebSocketUri = requestDto.WebsocketUri;
      },
      options =>
      {
        options.InstanceId = _desktopClientOptions.CurrentValue.InstanceId;
      });

    builder.AddRemoteControlPlatformServices();
    builder.ConfigureRemoteControlHostValidation();
    return builder;
  }
}
