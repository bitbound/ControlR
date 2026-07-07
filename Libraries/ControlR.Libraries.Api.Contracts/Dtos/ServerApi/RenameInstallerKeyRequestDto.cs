namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi;

public record RenameInstallerKeyRequestDto(
    Guid Id,
    string FriendlyName,
    Guid? TenantId = null,
    Guid? UserId = null,
    bool? IsTenantAdmin = null);
