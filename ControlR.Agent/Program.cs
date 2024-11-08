using ControlR.Agent.Startup;
using System.CommandLine;
using System.CommandLine.Parsing;

var rootCommand = new RootCommand("Open-source remote control agent.")
{
    CommandProvider.GetInstallCommand(args),
    CommandProvider.GetRunCommand(args),
    CommandProvider.GetUninstallCommand(args),
    CommandProvider.GetEchoDesktopCommand(args)
};

return await rootCommand.InvokeAsync(args);