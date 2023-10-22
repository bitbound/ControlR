namespace ControlR.Agent.Interfaces;

public interface IAgentInstaller
{
    Task Install(
        string? authorizedPublicKey = null,
        int vncPort = 5900,
        bool autoInstallVnc = true);

    Task Uninstall();
}