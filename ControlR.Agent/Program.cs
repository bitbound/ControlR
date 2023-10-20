using Microsoft.AspNetCore.SignalR.Client;
using System.CommandLine;
using System.CommandLine.Parsing;
using ControlR.Agent.Startup;

var rootCommand = new RootCommand("Provides zero-trust remote control and remote administration.")
{
    CommandProvider.GetInstallCommand(args),
    CommandProvider.GetRunCommand(args),
    CommandProvider.GetUninstallCommand(args),
    CommandProvider.GetSidecarCommand(args)
};

return await rootCommand.InvokeAsync(args);