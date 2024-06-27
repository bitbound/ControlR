namespace ControlR.Agent.Interfaces;

public interface IAgentInstaller
{
    Task Install(
        Uri? serverUri = null,
        string? authorizedPublicKey = null,
        string? label = null);

    Task Uninstall();
}