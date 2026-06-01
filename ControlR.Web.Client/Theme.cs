using ControlR.Libraries.Branding;

namespace ControlR.Web.Client;

public static class Theme
{
  public static PaletteDark DarkPalette { get; } = new PaletteDark
  {
    Primary = $"#{BrandingConstants.PrimaryColorDark}",
    Secondary = $"#{BrandingConstants.SecondaryColorDark}",
    Tertiary = $"#{BrandingConstants.TertiaryColorDark}",
    Info = $"#{BrandingConstants.InfoColorDark}",
    Success = $"#{BrandingConstants.SuccessColorDark}",
    Warning = $"#{BrandingConstants.WarningColorDark}",
    Error = $"#{BrandingConstants.ErrorColorDark}",
    Dark = "#212121",

    TextPrimary = "rgba(255, 255, 255, 0.87)",
    TextSecondary = "rgba(255, 255, 255, 0.60)",
    TextDisabled = "rgba(255, 255, 255, 0.38)",

    ActionDefault = "rgba(255, 255, 255, 0.87)",
    ActionDisabled = "rgba(255, 255, 255, 0.38)",
    
    Background = "#121212",
    BackgroundGray = "#1E1E1E",
    Surface = "#1E1E1E",
    
    AppbarBackground = "#1E1E1E",
    AppbarText = "rgba(255, 255, 255, 0.87)",

    DrawerBackground = "#1E1E1E",
    DrawerText = "rgba(255, 255, 255, 0.87)",

    Divider = "rgba(255, 255, 255, 0.12)",
    DividerLight = "rgba(255, 255, 255, 0.06)",

    TableLines = "rgba(255, 255, 255, 0.12)",
    TableStriped = "rgba(255, 255, 255, 0.02)",
    TableHover = "rgba(255, 255, 255, 0.04)",

    LinesDefault = "rgba(255, 255, 255, 0.12)",
    LinesInputs = "rgba(255, 255, 255, 0.30)",

    OverlayDark = "rgba(0, 0, 0, 0.5)",
    OverlayLight = "rgba(0, 0, 0, 0.25)"
  };

  public static PaletteLight LightPalette { get; } = new PaletteLight
  {
    Primary = $"#{BrandingConstants.PrimaryColorLight}",
    Secondary = $"#{BrandingConstants.SecondaryColorLight}",
    Tertiary = $"#{BrandingConstants.TertiaryColorLight}",
    Info = $"#{BrandingConstants.InfoColorLight}",
    Success = $"#{BrandingConstants.SuccessColorLight}",
    Warning = $"#{BrandingConstants.WarningColorLight}",
    Error = $"#{BrandingConstants.ErrorColorLight}",
    Dark = "#424242",

    TextPrimary = "rgba(0, 0, 0, 0.87)",
    TextSecondary = "rgba(0, 0, 0, 0.60)",
    TextDisabled = "rgba(0, 0, 0, 0.38)",

    ActionDefault = "rgba(0, 0, 0, 0.87)",
    ActionDisabled = "rgba(0, 0, 0, 0.38)",

    Background = "#FAFAFA",
    BackgroundGray = "#F5F5F5",
    Surface = "#FFFFFF",

    AppbarBackground = "#F0F0F0",
    AppbarText = "rgba(0, 0, 0, 0.87)",

    DrawerBackground = "#FFFFFF",
    DrawerText = "rgba(0, 0, 0, 0.87)",

    Divider = "rgba(0, 0, 0, 0.12)",
    DividerLight = "rgba(0, 0, 0, 0.06)",

    TableLines = "rgba(0, 0, 0, 0.12)",
    TableStriped = "rgba(0, 0, 0, 0.02)",
    TableHover = "rgba(0, 0, 0, 0.04)",

    LinesDefault = "rgba(0, 0, 0, 0.12)",
    LinesInputs = "rgba(0, 0, 0, 0.42)",

    OverlayDark = "rgba(0, 0, 0, 0.32)",
    OverlayLight = "rgba(0, 0, 0, 0.12)"
  };
}
