using ControlR.Agent.Startup;
using System.CommandLine;

var rootCommand = new RootCommand("Open-source remote control agent.")
{
    CommandProvider.GetInstallCommand(args),
    CommandProvider.GetRunCommand(args),
    CommandProvider.GetUninstallCommand(args),
};

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();