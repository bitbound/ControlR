namespace ControlR.Agent.Interfaces;

public interface IAgentInstaller
{
    Task Install(string? authorizedPublicKey = null);
    Task Uninstall();
}
