﻿using Bitbound.SimpleMessenger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using ControlR.Streamer;
using ControlR.Streamer.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR.Client;
using ControlR.Streamer.Extensions;
using ControlR.Libraries.DevicesCommon.Extensions;
using ControlR.Libraries.Shared.Helpers;

var sessionIdOption = new Option<Guid>(
    ["-s", "--session-id"],
    "The session ID for this streaming session.")
{
  IsRequired = true,
};

var appDataFolderOption = new Option<string>(
    ["-d", "--data-folder"],
    "The folder name in 'C:\\ProgramData\\ControlR\\' under which logs and other data will be written.")
{
  IsRequired = true
};

appDataFolderOption.AddValidator(result =>
{
  var folderValue = result.GetValueForOption(appDataFolderOption);
  Guard.IsNotNull(folderValue, nameof(folderValue));

  var invalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
  if (folderValue.Any(c => invalidCharacters.Contains(c)))
  {
    result.ErrorMessage = "The app data folder name contains invalid characters.";
  }
});

var websocketUriOption = new Option<Uri>(
    ["-w", "--websocket-uri"],
    "The websocket URI (including scheme and port) that the streamer should use for video (e.g. wss://my.example.com[:8080]). " +
    "The port can be ommitted for 80 (http) and 443 (https).")
{
  IsRequired = true
};

var notifyUserOption = new Option<bool>(
    ["-n", "--notify-user"],
    "Whether to notify the user when a remote control session starts.");

var viewerNameOption = new Option<string?>(
    ["-vn", "--viewer-name"],
    "The name of the viewer requesting the session.");

var rootCommand = new RootCommand("The remote control desktop streamer and input simulator for ControlR.")
{
    appDataFolderOption,
    websocketUriOption,
    notifyUserOption,
    sessionIdOption,
    viewerNameOption,
};

rootCommand.SetHandler(async (appDataFolder, websocketUri, notifyUser, sessionId, viewerName) =>
{
  var builder = Host.CreateApplicationBuilder(args);
  var configuration = builder.Configuration;
  var services = builder.Services;
  var logging = builder.Logging;

  var appsettingsFile = SystemEnvironment.Instance.IsDebug ? "appsettings.Development.json" : "appsettings.json";
  configuration
    .AddJsonFile(appsettingsFile, true, true)
    .AddJsonFile(PathConstants.GetAppSettingsPath(appDataFolder), true, true)
    .AddEnvironmentVariables();

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
  services.AddSingleton<IClipboardManager, ClipboardManager>();
  services.AddSingleton<IDelayer, Delayer>();
  services.AddScreenCapturer();
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

}, appDataFolderOption, websocketUriOption, notifyUserOption, sessionIdOption, viewerNameOption);

var exitCode = await rootCommand.InvokeAsync(args);
Environment.Exit(exitCode);