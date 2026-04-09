using ControlR.Libraries.Api.Contracts.Enums;

namespace ControlR.Web.Client.Extensions;

internal static class UserPreferenceMappings
{
  public static ThemeMode ToClientThemeMode(this UserPreferenceThemeMode value)
  {
    return value switch
    {
      UserPreferenceThemeMode.Auto => ThemeMode.Auto,
      UserPreferenceThemeMode.Light => ThemeMode.Light,
      UserPreferenceThemeMode.Dark => ThemeMode.Dark,
      _ => ThemeMode.Auto
    };
  }

  public static ViewMode ToClientViewMode(this UserPreferenceViewMode value)
  {
    return value switch
    {
      UserPreferenceViewMode.Fit => ViewMode.Fit,
      UserPreferenceViewMode.Stretch => ViewMode.Stretch,
      UserPreferenceViewMode.Scale => ViewMode.Scale,
      _ => ViewMode.Fit
    };
  }

  public static UserPreferenceThemeMode ToUserPreferenceThemeMode(this ThemeMode value)
  {
    return value switch
    {
      ThemeMode.Auto => UserPreferenceThemeMode.Auto,
      ThemeMode.Light => UserPreferenceThemeMode.Light,
      ThemeMode.Dark => UserPreferenceThemeMode.Dark,
      _ => UserPreferenceThemeMode.Auto
    };
  }

  public static UserPreferenceViewMode ToUserPreferenceViewMode(this ViewMode value)
  {
    return value switch
    {
      ViewMode.Fit => UserPreferenceViewMode.Fit,
      ViewMode.Stretch => UserPreferenceViewMode.Stretch,
      ViewMode.Scale => UserPreferenceViewMode.Scale,
      _ => UserPreferenceViewMode.Fit
    };
  }
}