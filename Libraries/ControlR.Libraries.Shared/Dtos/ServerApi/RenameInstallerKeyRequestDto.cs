namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public record RenameInstallerKeyRequestDto(
    Guid Id,
    string FriendlyName);
