using Bitbound.SimpleMessenger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using ControlR.Streamer.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR.Client;
using ControlR.Libraries.DevicesCommon.Extensions;

var sessionIdOption = new Option<Guid>(
  "SessionId",
  ["-s", "--session-id"])
{
  Required = true,
  Description = "The session ID for this streaming session."
};

var appDataFolderOption = new Option<string>(
  "FolderData",
  ["-d", "--data-folder"])
{
  Required = true,
  Description = "The folder name in 'C:\\ProgramData\\ControlR\\' under which logs and other data will be written."
};

appDataFolderOption.Validators.Add(result =>
{
  var folderValue = result.GetRequiredValue(appDataFolderOption);
  var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
  if (folderValue.Any(c => invalidCharacters.Contains(c)))
  {
    result.AddError("The app data folder name contains invalid characters.");
  }
});

var websocketUriOption = new Option<Uri>(
  "WebSocketUri",
  ["-w", "--websocket-uri"])
{
  Required = true,
  Description = 
    "The websocket URI (including scheme and port) that the streamer should " +
    "use for video (e.g. wss://my.example.com[:8080]). " +
    "The port can be ommitted for 80 (http) and 443 (https).",
  CustomParser = result =>
  {
    if (result.Tokens.Count == 0)
    {
      throw new ArgumentException(
        "The websocket URI is required. Please provide a valid URI including the scheme (e.g. 'wss://').");
    }
    var uriArg = result.Tokens[0].Value;
    if (!Uri.TryCreate(uriArg, UriKind.Absolute, out var uri))
    {
      throw new ArgumentException(
        $"The websocket URI '{uriArg}' is not a valid absolute URI. " +
        "Please provide a valid URI including the scheme (e.g. 'wss://').");
    }
    return uri;
  }
};

var notifyUserOption = new Option<bool>(
  "NotifyUser",
  ["-n", "--notify-user"])
{
  Description = "Whether to notify the user when a remote control session starts."
};

var viewerNameOption = new Option<string?>(
  "ViewerName",
  ["-vn", "--viewer-name"])
{
  Description = "The name of the viewer requesting the session."
};

var rootCommand = new RootCommand("The remote control desktop streamer and input simulator for ControlR.")
{
    appDataFolderOption,
    websocketUriOption,
    notifyUserOption,
    sessionIdOption,
    viewerNameOption,
};


rootCommand.SetAction(async parseResult =>
{
  var appDataFolder = parseResult.GetRequiredValue(appDataFolderOption);
  var websocketUri = parseResult.GetRequiredValue(websocketUriOption);
  var notifyUser = parseResult.GetValue(notifyUserOption);
  var sessionId = parseResult.GetRequiredValue(sessionIdOption);
  var viewerName = parseResult.GetValue(viewerNameOption);

  var builder = Host.CreateApplicationBuilder(args);
  var configuration = builder.Configuration;
  var services = builder.Services;
  var logging = builder.Logging;

  configuration.AddEnvironmentVariables();

  services.Configure<StartupOptions>(options =>
  {
    options.WebSocketUri = websocketUri;
    options.NotifyUser = notifyUser;
    options.ViewerName = viewerName;
    options.SessionId = sessionId;
  });

  services.AddSingleton<IProcessManager, ProcessManager>();
  services.AddSingleton<IStreamerStreamingClient, StreamerStreamingClient>();
  services.AddSingleton(WeakReferenceMessenger.Default);
  services.AddSingleton(TimeProvider.System);
  services.AddSingleton<IWin32Interop, Win32Interop>();
  services.AddSingleton<IToaster, Toaster>();
  services.AddSingleton<IDesktopCapturer, DesktopCapturer>();
  services.AddSingleton<ICaptureMetrics, CaptureMetrics>();
  services.AddSingleton<IInputSimulator, InputSimulatorWindows>();
  services.AddSingleton<IMemoryProvider, MemoryProvider>();
  services.AddSingleton<ISystemEnvironment, SystemEnvironment>();
  services.AddSingleton<IClipboardManager, ClipboardManager>();
  services.AddSingleton<IDelayer, Delayer>();
  services
      .AddSingleton<IBitmapUtility, BitmapUtility>()
      .AddSingleton<IScreenGrabber, ScreenGrabber>()
      .AddSingleton<IDxOutputGenerator, DxOutputGenerator>();
  services.AddTransient<IHubConnectionBuilder, HubConnectionBuilder>();
  services.AddHostedService<SystemEventHandler>();
  services.AddHostedService<HostLifetimeEventResponder>();
  services.AddHostedService<InputDesktopReporter>();
  services.AddHostedService<CursorWatcher>();
  services.AddHostedService<DtoHandler>();
  services.AddHostedService(x => x.GetRequiredService<IStreamerStreamingClient>());

  builder.BootstrapSerilog(
    logFilePath: PathConstants.GetLogsPath(appDataFolder),
    logRetention: TimeSpan.FromDays(7));

  var host = builder.Build();
  await host.RunAsync();
});

return await rootCommand
  .Parse(args)
  .InvokeAsync();