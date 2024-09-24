namespace ControlR.Agent.Interfaces;

public interface IAgentInstaller
{
    Task Install(Uri? serverUri = null);

    Task Uninstall();
}