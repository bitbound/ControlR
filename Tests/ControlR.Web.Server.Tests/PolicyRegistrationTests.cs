using System.Reflection;
using ControlR.Web.Server.Authz.Policies;
using Microsoft.AspNetCore.Authorization;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Meta-test: every policy referenced by <c>[Authorize(Policy = ...)]</c> in the server
/// assembly must be a registered key in <see cref="PermissionPolicies.Definitions"/>
/// (or, for device-resource policies, <see cref="DeviceResourcePolicies.PolicyToPermission"/>).
/// A referenced-but-unregistered policy throws at runtime when the endpoint is hit, so a
/// future contributor adding a policy without registering it is caught here at build time.
/// </summary>
public class PolicyRegistrationTests
{
  private static HashSet<string> AllRegisteredKeys => [.. PermissionPolicyKeys, .. DeviceResourcePolicyKeys];
  private static HashSet<string> DeviceResourcePolicyKeys { get; } = [.. DeviceResourcePolicies.PolicyToPermission.Keys];
  private static HashSet<string> PermissionPolicyKeys { get; } = [.. PermissionPolicies.Definitions.Keys];
  private static Assembly ServerAssembly { get; } = typeof(DeviceResourcePolicies).Assembly;

  [Fact]
  public void EveryAuthorizePolicy_IsRegistered()
  {
    var referencedPolicies = ServerAssembly
      .GetTypes()
      .SelectMany(type => GetAuthorizePolicyNames(type))
      .ToHashSet();

    Assert.NotEmpty(referencedPolicies);

    var missingPolicies = referencedPolicies
      .Where(policy => !AllRegisteredKeys.Contains(policy!))
      .OrderBy(policy => policy)
      .ToList();

    Assert.True(
      missingPolicies.Count == 0,
      $"The following [Authorize(Policy=...)] policies are referenced but not registered: {string.Join(", ", missingPolicies)}");
  }

  [Fact]
  public void EveryDeviceResourcePolicyConstant_IsRegistered()
  {
    // The DeviceResourcePolicies public consts are the backing for
    // AuthorizeAsync(..., DeviceResourcePolicies.X) resource checks. Each const's value is
    // the key in PolicyToPermission, which maps it to a device permission. Every constant
    // must be present, or an endpoint referencing a missing one throws at runtime.
    var devicePolicyConstants = typeof(DeviceResourcePolicies)
      .GetFields(BindingFlags.Public | BindingFlags.Static)
      .Where(field => field.IsLiteral && !field.IsInitOnly)
      .Select(field => (string?)field.GetRawConstantValue())
      .Where(value => !string.IsNullOrWhiteSpace(value))
      .Distinct()
      .ToHashSet();

    var registeredDevicePolicies = DeviceResourcePolicies.PolicyToPermission.Keys.ToHashSet();

    var missing = devicePolicyConstants.Except(registeredDevicePolicies).Distinct().OrderBy(x => x).ToList();

    Assert.True(
      missing.Count == 0,
      $"The following DeviceResourcePolicies constants are not registered in PolicyToPermission: {string.Join(", ", missing)}");
  }

  private static IEnumerable<string> GetAuthorizePolicyNames(Type type)
  {
    // Controller classes apply [Authorize(Policy=...)] at the class and/or action level.
    // Both must be checked, or an unregistered policy on a method would slip through.
    var typeAttributes = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
      .Select(attr => attr.Policy);
    var methodAttributes = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
      .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
      .Select(attr => attr.Policy);

    return typeAttributes
      .Concat(methodAttributes)
      .Where(policy => !string.IsNullOrWhiteSpace(policy))!;
  }
}
