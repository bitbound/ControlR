namespace ControlR.Libraries.Branding.Dtos;

/// <summary>
/// Icon URLs for customization.
/// </summary>
public record CustomizationImagesDto(
    string? PngUri = null,
    string? IcoUri = null,
    string? CompanyLogoPngUri = null);
