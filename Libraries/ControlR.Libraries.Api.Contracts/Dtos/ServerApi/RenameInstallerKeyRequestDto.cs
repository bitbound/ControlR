namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record RenameInstallerKeyRequestDto(
    Guid Id,
    string FriendlyName);
