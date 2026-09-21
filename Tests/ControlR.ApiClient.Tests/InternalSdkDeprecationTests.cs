using System.Reflection;
using System.Text.RegularExpressions;
using ControlR.ApiClient.Interfaces.Internal;

namespace ControlR.ApiClient.Tests;

/// <summary>
/// Pins the [Obsolete] markers on zero-caller internal SDK methods and checks that each deprecation
/// note names a V1 SDK method that actually exists. A method that still had a live caller would fail
/// the build (CS0618 is an error here), so the surviving markers prove the methods are caller-free.
/// </summary>
public sealed partial class InternalSdkDeprecationTests
{
  private const string InternalNamespace = "ControlR.ApiClient.Interfaces.Internal";

  private static readonly string[] _deprecatedInternalMethods =
  [
    "IDeviceFileSystemApi.ValidateFilePath",
    "IDeviceTagsApi.AddDeviceTag",
    "IDeviceTagsApi.RemoveDeviceTag",
    "IDevicesApi.DeleteManyDevices",
    "IDevicesApi.GetDeviceSummaries",
    "IEffectiveUserPreferencesApi.GetEffectiveUserPreferences",
    "IInvitesApi.CreateTenantInvite",
    "IInvitesApi.DeleteTenantInvite",
    "IInvitesApi.GetPendingTenantInvites",
    "IPersonalAccessTokensApi.CreatePersonalAccessToken",
    "IPersonalAccessTokensApi.DeletePersonalAccessToken",
    "IPersonalAccessTokensApi.GetPersonalAccessTokens",
    "IPersonalAccessTokensApi.UpdatePersonalAccessToken",
    "ITagsApi.CreateTag",
    "ITagsApi.DeleteTag",
    "ITagsApi.GetAllTags",
    "ITagsApi.RenameTag",
    "ITenantSettingsApi.DeleteTenantSetting",
    "ITenantSettingsApi.GetTenantSetting",
    "ITenantSettingsApi.GetTenantSettings",
    "ITenantSettingsApi.SetTenantSetting",
    "ITenantSettingsApi.SetTenantSettings",
    "IUserPreferencesApi.GetUserPreference",
    "IUserPreferencesApi.SetUserPreference",
    "IUserPreferencesApi.SetUserPreferences",
    "IUserServerSettingsApi.GetDecommissionStatus",
    "IUserStorageApi.DeleteUserStorageItem",
    "IUserStorageApi.GetUserStorageItem",
    "IUserStorageApi.SetUserStorageItem",
    "IUsersApi.AdminResetPassword",
    "IUsersApi.CreateUser",
    "IUsersApi.CreateUserPersonalAccessToken",
    "IUsersApi.DeleteUser",
    "IUsersApi.DeleteUserPersonalAccessToken",
    "IUsersApi.GetAllUsers",
    "IUsersApi.GetUserPersonalAccessTokens",
    "IUsersApi.UpdateUserPersonalAccessToken",
    "IVersionApi.GetCurrentAgentVersion",
    "IVersionApi.GetCurrentServerVersion",
  ];

  [Fact]
  public void DeprecatedInternalMethods_AreAllMarkedObsolete()
  {
    var actual = InternalMethodsByDeprecation();

    var missing = _deprecatedInternalMethods
      .Where(name => !actual.Contains(name))
      .OrderBy(name => name, StringComparer.Ordinal)
      .ToArray();

    Assert.True(
      missing.Length == 0,
      "Internal SDK methods that should carry [Obsolete] naming their V1 twin, but do not: " +
      string.Join(" | ", missing));
  }

  [Fact]
  public void ObsoleteInternalMethods_NameAnExistingV1SdkMethod()
  {
    var assembly = typeof(IDevicesApi).Assembly;
    var problems = new List<string>();

    foreach (var type in InternalInterfaceTypes(assembly))
    {
      foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
      {
        var note = method.GetCustomAttribute<ObsoleteAttribute>()?.Message;
        if (note is null)
        {
          continue;
        }

        var match = TwinReferenceRegex().Match(note);
        if (!match.Success)
        {
          problems.Add($"{type.Name}.{method.Name} note names no ControlrApi.V1.<Sub>.<Method> twin");
          continue;
        }

        var sub = match.Groups[1].Value;
        var twin = match.Groups[2].Value;
        var v1Type = assembly.GetType($"ControlR.ApiClient.Interfaces.V1.I{sub}Api");

        if (v1Type is null)
        {
          problems.Add($"{type.Name}.{method.Name} names twin I{sub}Api, which does not exist");
          continue;
        }

        if (!v1Type.GetMethods().Any(candidate => candidate.Name == twin))
        {
          problems.Add($"{type.Name}.{method.Name} names twin I{sub}Api.{twin}, which does not exist");
        }
      }
    }

    Assert.True(
      problems.Count == 0,
      "Broken [Obsolete] deprecation pointers on internal SDK methods: " + string.Join(" | ", problems));
  }

  private static IEnumerable<Type> InternalInterfaceTypes(Assembly assembly)
  {
    return assembly
      .GetTypes()
      .Where(type => type.IsInterface && type.Namespace == InternalNamespace)
      .OrderBy(type => type.Name, StringComparer.Ordinal);
  }

  private static HashSet<string> InternalMethodsByDeprecation()
  {
    var assembly = typeof(IDevicesApi).Assembly;
    var names = new HashSet<string>(StringComparer.Ordinal);

    foreach (var type in InternalInterfaceTypes(assembly))
    {
      foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
      {
        if (method.GetCustomAttribute<ObsoleteAttribute>() is not null)
        {
          names.Add($"{type.Name}.{method.Name}");
        }
      }
    }

    return names;
  }

  [GeneratedRegex(@"ControlrApi\.V1\.(\w+)\.(\w+)")]
  private static partial Regex TwinReferenceRegex();
}
