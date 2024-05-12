using ControlR.Agent.Startup;
using Microsoft.AspNetCore.SignalR.Client;
using System.CommandLine;
using System.CommandLine.Parsing;

var rootCommand = new RootCommand("Provides zero-trust remote control and remote administration.")
{
    CommandProvider.GetInstallCommand(args),
    CommandProvider.GetRunCommand(args),
    CommandProvider.GetUninstallCommand(args),
    CommandProvider.GetEchoDesktopCommand(args)
};

return await rootCommand.InvokeAsync(args);