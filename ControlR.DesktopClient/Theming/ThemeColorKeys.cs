namespace ControlR.DesktopClient.Theming;

public static class ThemeColorKeys
{
  public static string GetContastTextResourceKey(ThemeColor themeColor)
  {
    return themeColor switch
    {
      ThemeColor.Primary => "PrimaryContrastTextColor",
      ThemeColor.Secondary => "SecondaryContrastTextColor",
      ThemeColor.Tertiary => "TertiaryContrastTextColor",
      ThemeColor.Info => "InfoContrastTextColor",
      ThemeColor.Success => "SuccessContrastTextColor",
      ThemeColor.Warning => "WarningContrastTextColor",
      ThemeColor.Error => "ErrorContrastTextColor",
      ThemeColor.Dark => "DarkContrastTextColor",
      _ => "PrimaryContrastTextColor"
    };
  }

  public static string GetResourceKey(ThemeColor themeColor)
  {
    return themeColor switch
    {
      ThemeColor.Primary => "PrimaryColor",
      ThemeColor.Secondary => "SecondaryColor",
      ThemeColor.Tertiary => "TertiaryColor",
      ThemeColor.Info => "InfoColor",
      ThemeColor.Success => "SuccessColor",
      ThemeColor.Warning => "WarningColor",
      ThemeColor.Error => "ErrorColor",
      ThemeColor.Dark => "DarkColor",
      _ => "PrimaryColor"
    };
  }
}