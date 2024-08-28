using Bitbound.SimpleMessenger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using ControlR.Streamer;
using ControlR.Libraries.ScreenCapture.Extensions;
using ControlR.Streamer.Services;
using ControlR.Libraries.Shared.Services.Buffers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

var authorizedKeyOption = new Option<string>(
    ["-a", "--authorized-key"],
    "The public key of the viewer who's authorized to join this session.")
{
    IsRequired = true,
};

var sessionIdOption = new Option<Guid>(
    ["-s", "--session-id"],
    "The session ID for this streaming session.")
{
    IsRequired = true,
};

var originUriOption = new Option<Uri>(
    ["-o", "--origin"],
    "The origin URI (including scheme and port) that the streamer should use for data (e.g. https://my.example.com[:8080]). " +
    "The port can be ommitted for 80 (http) and 443 (https).")
{
    IsRequired = true
};

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

var viewerIdOption = new Option<string>(
    ["-vi", "--viewer-id"],
    "The connection ID of the viewer who is requesting the streaming session.")
{
    IsRequired = true
};

var viewerNameOption = new Option<string?>(
    ["-vn", "--viewer-name"],
    "The name of the viewer requesting the session.");

var rootCommand = new RootCommand("The remote control desktop streamer and input simulator for ControlR.")
{
    authorizedKeyOption,
    originUriOption,
    websocketUriOption,
    viewerIdOption,
    notifyUserOption,
    sessionIdOption,
    viewerNameOption,
};

rootCommand.SetHandler(async (authorizedKey, originUri, websocketUri, viewerConnectionId, notifyUser, sessionId, viewerName) =>
{
    var host = Host.CreateDefaultBuilder(args)
        .UseConsoleLifetime()
        .ConfigureAppConfiguration(builder =>
        {
            var appsettingsFile = EnvironmentHelper.Instance.IsDebug ? "appsettings.Development.json" : "appsettings.json";
            builder
                .AddJsonFile(PathConstants.GetAppSettingsPath(originUri), true, true)
                .AddJsonFile(appsettingsFile, true, true)
                .AddEnvironmentVariables();
        })
        .ConfigureServices(services =>
        {
            services.Configure<StartupOptions>(options =>
            {
                options.AuthorizedKey = authorizedKey;
                options.ServerOrigin = originUri;
                options.WebSocketUri = websocketUri;
                options.NotifyUser = notifyUser;
                options.ViewerConnectionId = viewerConnectionId;
                options.ViewerName = viewerName;
                options.SessionId = sessionId;
            });

            services.AddSingleton<IProcessManager, ProcessManager>();
            services.AddSingleton<IStreamerStreamingClient, StreamerStreamingClient>();
            services.AddSingleton(WeakReferenceMessenger.Default);
            services.AddSingleton<IWin32Interop, Win32Interop>();
            services.AddSingleton<IToaster, Toaster>();
            services.AddSingleton<IDisplayManager, DisplayManager>();
            services.AddSingleton<IInputSimulator, InputSimulatorWindows>();
            services.AddSingleton<IMemoryProvider, MemoryProvider>();
            services.AddSingleton<IKeyProvider, KeyProvider>();
            services.AddSingleton<ISystemTime, SystemTime>();
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
            services.AddHostedService(x => x.GetRequiredService<IClipboardManager>());
        })
        .BootstrapSerilog(
            logFilePath: PathConstants.GetLogsPath(originUri), 
            logRetention: TimeSpan.FromDays(7))
        .Build();

    await host.RunAsync();

}, authorizedKeyOption, originUriOption, websocketUriOption, viewerIdOption, notifyUserOption, sessionIdOption, viewerNameOption);

var exitCode = await rootCommand.InvokeAsync(args);
Environment.Exit(exitCode);