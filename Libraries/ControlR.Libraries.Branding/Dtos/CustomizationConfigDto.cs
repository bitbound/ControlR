namespace ControlR.Libraries.Branding.Dtos;

/// <summary>
/// DTO representing the customization config fetched from <c>customization_config_url</c>.
/// All properties are optional; when null, <see cref="BrandingConstants"/> defaults are used.
/// </summary>
public record CustomizationConfigDto(
    string? BrandName = null,
    string? Publisher = null,
    string? Version = null,
    CustomizationColorsDto? Colors = null,
    CustomizationImagesDto? Images = null);
