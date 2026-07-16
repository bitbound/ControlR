namespace ControlR.Web.Server.Tests.Helpers;

/// <summary>
/// Shared helpers for building test configuration dictionaries.
/// </summary>
internal static class TestConfigHelper
{
  private static readonly Dictionary<string, string?> _base = new()
  {
    ["AppOptions:DisableEmailSending"] = "true",
  };

  public static Dictionary<string, string?> GetBaseConfig() => new(_base);

  public static Dictionary<string, string?> GetSelfRegistrationDisabledConfig(
    bool enablePublicRegistration = false) =>
    new(_base) { ["AppOptions:DisableFirstUserSelfRegistration"] = "true", ["AppOptions:EnablePublicRegistration"] = $"{enablePublicRegistration}" };

  public static Dictionary<string, string?> GetSelfRegistrationEnabledConfig(
    bool enablePublicRegistration = false) =>
    new(_base) { ["AppOptions:DisableFirstUserSelfRegistration"] = "false", ["AppOptions:EnablePublicRegistration"] = $"{enablePublicRegistration}" };
}
