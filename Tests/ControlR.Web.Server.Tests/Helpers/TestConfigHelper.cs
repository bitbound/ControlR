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

  public static Dictionary<string, string?> BaseConfig() => new(_base);

  public static Dictionary<string, string?> SelfRegistrationDisabledConfig(
    bool enablePublicRegistration = false) =>
    new(_base) { ["AppOptions:EnableFirstUserSelfRegistration"] = "false", ["AppOptions:EnablePublicRegistration"] = $"{enablePublicRegistration}" };

  public static Dictionary<string, string?> SelfRegistrationEnabledConfig(
    bool enablePublicRegistration = false) =>
    new(_base) { ["AppOptions:EnableFirstUserSelfRegistration"] = "true", ["AppOptions:EnablePublicRegistration"] = $"{enablePublicRegistration}" };
}