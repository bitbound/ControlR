namespace ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.TenantSettings;

/// <summary>
/// A single setting write. A null or whitespace <c>Value</c> clears the setting.
/// </summary>
public record TenantSettingRequestDto(string Name, string Value);
