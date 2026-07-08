namespace ControlR.Libraries.Api.Contracts.Dtos.Internal;

public record RenameInstallerKeyRequestDto(
    Guid Id,
    string FriendlyName);
