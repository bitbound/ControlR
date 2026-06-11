namespace ControlR.Libraries.Branding.Dtos;

/// <summary>
/// Seven semantic colors for a theme palette.
/// Hex values without leading '#' (e.g. "2196F3").
/// </summary>
public record CustomizationColorPaletteDto(
    string? Primary = null,
    string? Secondary = null,
    string? Tertiary = null,
    string? Info = null,
    string? Success = null,
    string? Warning = null,
    string? Error = null);
