using Bitbound.SimpleMessenger;
using ControlR.Devices.Common.Services;
using ControlR.Shared.Extensions;
using ControlR.Streamer.Sidecar;
using ControlR.Streamer.Sidecar.Options;
using ControlR.Streamer.Sidecar.Services;
using ControlR.Streamer.Sidecar.Services.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

var streamerPipeOption = new Option<string>(
    ["-s", "--streamer-pipe"],
    "The streamer's pipe name to which the watcher should connect.")
{
    IsRequired = true
};

streamerPipeOption.AddValidator(result =>
{
    var parsedValue = result.GetValueOrDefault<string>();
    if (string.IsNullOrWhiteSpace(parsedValue))
    {
        result.ErrorMessage = "Streamer pipe name cannot be empty.";
    }
});

var parentIdOption = new Option<int>(
    [ "-p", "--parent-id" ],
    "The calling process's ID.")
{
    IsRequired = true
};

parentIdOption.AddValidator(result =>
{
    var parsedValue = result.GetValueOrDefault<int>();
    if (parsedValue < 1)
    {
        result.ErrorMessage = "Parent process ID must be a positive integer.";
    }
});

var rootCommand = new RootCommand("Watches for desktop changes (winlogon/UAC) for the streamer process.")
{
    streamerPipeOption,
    parentIdOption
};

rootCommand.SetHandler(async (streamerPipeName, parentProcessId) =>
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices(services => 
        {
            services.Configure<StartupOptions>(options =>
            {
                options.ParentProcessId = parentProcessId;
                options.StreamerPipeName = streamerPipeName;
            });

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 0, 6000))
            {
                services.AddSingleton<IStreamerIpcConnection, StreamerIpcConnection>();
                services.AddSingleton<IInputSimulator, InputSimulator>();
                services.AddSingleton(WeakReferenceMessenger.Default);
                services.AddSingleton<IProcessManager, ProcessManager>();
                services.AddHostedService<InputDesktopReporter>();
            }
        })
        .ConfigureLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            logging.AddProvider(new FileLoggerProvider(
                version,
                () => LoggingConstants.LogPath,
                TimeSpan.FromDays(7)));
            logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
        })
        .BootstrapSerilog(LoggingConstants.LogPath, TimeSpan.FromDays(7))
        .Build();

    var ipcConnection = host.Services.GetRequiredService<IStreamerIpcConnection>();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await ipcConnection.Connect(streamerPipeName, cts.Token);
    await host.RunAsync();

}, streamerPipeOption, parentIdOption);

await rootCommand.InvokeAsync(args);