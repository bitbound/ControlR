using ControlR.Agent.Startup;
using System.CommandLine;

var rootCommand = new RootCommand("Open-source remote control agent.")
{
  CommandProvider.GetRunCommand(args),
  CommandProvider.GetStartServiceCommand(args),
  CommandProvider.GetStopServiceCommand(args),
  CommandProvider.GetUninstallCommand(args),
};

var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();
