namespace ControlR.Libraries.Branding.Dtos;

/// <summary>
/// Dark and light theme color palettes.
/// </summary>
public record CustomizationColorsDto(
    CustomizationColorPaletteDto? Dark = null,
    CustomizationColorPaletteDto? Light = null);
