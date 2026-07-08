namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

public record RenameInstallerKeyRequestDto(
    Guid Id,
    string FriendlyName);
