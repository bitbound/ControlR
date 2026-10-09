using System.Reflection;
using ControlR.Web.Server.Authz.Policies;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Meta-test: every policy referenced by <c>[Authorize(Policy = ...)]</c> in the server
/// assembly must resolve from the authorization policy provider. A referenced-but-unregistered
/// policy throws at runtime when the endpoint is hit, so a future contributor adding a policy
/// without registering it is caught here at build time.
/// </summary>
/// <remarks>
/// The check asks the provider rather than comparing against the permission registries, because
/// not every policy is a permission policy. An installed agent proves itself with a request
/// signature and holds no permission, so its policy is registered directly and appears in
/// neither <see cref="PermissionPolicies.Definitions"/> nor
/// <see cref="DeviceResourcePolicies.PolicyToPermission"/>.
/// </remarks>
public class PolicyRegistrationTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  private static Assembly ServerAssembly { get; } = typeof(DeviceResourcePolicies).Assembly;

  [Fact]
  public async Task EveryAuthorizePolicy_IsRegistered()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    var provider = testApp.Services.GetRequiredService<IAuthorizationPolicyProvider>();

    // Proves this check is capable of failing: an unregistered name resolves to no policy, so a
    // referenced-but-missing policy cannot slip through.
    Assert.Null(await provider.GetPolicyAsync($"unregistered-{Guid.NewGuid():N}"));

    var referencedPolicies = ServerAssembly
      .GetTypes()
      .SelectMany(type => GetAuthorizePolicyNames(type))
      .ToHashSet();

    Assert.NotEmpty(referencedPolicies);

    var missingPolicies = new List<string>();
    foreach (var policy in referencedPolicies)
    {
      if (await provider.GetPolicyAsync(policy!) is null)
      {
        missingPolicies.Add(policy!);
      }
    }

    Assert.True(
      missingPolicies.Count == 0,
      $"The following [Authorize(Policy=...)] policies are referenced but not registered: {string.Join(", ", missingPolicies.OrderBy(x => x, StringComparer.Ordinal))}");
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
