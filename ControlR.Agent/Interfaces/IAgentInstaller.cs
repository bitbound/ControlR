namespace ControlR.Agent.Interfaces;

public interface IAgentInstaller
{
    Task Install(
        Uri? serverUri = null,
        string? authorizedPublicKey = null,
        int? vncPort = null,
        bool? autoRunVnc = null);

    Task Uninstall();
}