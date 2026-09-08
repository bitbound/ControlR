using System.Collections.Frozen;
using System.Collections.Immutable;
using ControlR.Libraries.Shared.Comparers;
using ControlR.Web.Server.Authz.Permissions;

namespace ControlR.Web.Server.Services.Authorization;

public interface IPermissionEvaluator
{
  Task<PermissionEvaluationResult> Evaluate(
    PrincipalDescriptor principal,
    string permissionName,
    ResourceDescriptor resource,
    CancellationToken cancellationToken);
  Task<IReadOnlyDictionary<PermissionEvaluationRequest, PermissionEvaluationResult>> EvaluateBatch(
    PrincipalDescriptor principal,
    IReadOnlyList<PermissionEvaluationRequest> requests,
    CancellationToken cancellationToken);
  Task<IReadOnlyDictionary<string, PermissionEvaluationResult>> EvaluateMany(
    PrincipalDescriptor principal,
    IReadOnlyCollection<string> permissionNames,
    ResourceDescriptor resource,
    CancellationToken cancellationToken);
  Task<IReadOnlySet<string>> GetGrantedPolicies(
    PrincipalDescriptor principal,
    CancellationToken cancellationToken);
}

public sealed class PermissionEvaluator(
  IPermissionEvaluationContextLoader contextLoader,
  IPermissionDecisionEvaluator decisionEvaluator,
  IResourceDescriptorFactory resourceDescriptorFactory) : IPermissionEvaluator
{
  private readonly IPermissionEvaluationContextLoader _contextLoader = contextLoader;
  private readonly IPermissionDecisionEvaluator _decisionEvaluator = decisionEvaluator;
  private readonly IResourceDescriptorFactory _resourceDescriptorFactory = resourceDescriptorFactory;

  public async Task<PermissionEvaluationResult> Evaluate(
    PrincipalDescriptor principal,
    string permissionName,
    ResourceDescriptor resource,
    CancellationToken cancellationToken)
  {
    var context = await _contextLoader.Load(principal, cancellationToken);
    return _decisionEvaluator.Evaluate(context, permissionName, resource);
  }

  public async Task<IReadOnlyDictionary<PermissionEvaluationRequest, PermissionEvaluationResult>> EvaluateBatch(
    PrincipalDescriptor principal,
    IReadOnlyList<PermissionEvaluationRequest> requests,
    CancellationToken cancellationToken)
  {
    if (requests.Count == 0)
    {
      return ImmutableDictionary.Create<PermissionEvaluationRequest, PermissionEvaluationResult>(
        ReferenceEqualityComparer<PermissionEvaluationRequest>.Instance);
    }

    var context = await _contextLoader.Load(principal, cancellationToken);
    return requests.ToFrozenDictionary(
      request => request,
      request => _decisionEvaluator.Evaluate(context, request.PermissionName, request.Resource),
      ReferenceEqualityComparer<PermissionEvaluationRequest>.Instance);
  }

  public async Task<IReadOnlyDictionary<string, PermissionEvaluationResult>> EvaluateMany(
    PrincipalDescriptor principal,
    IReadOnlyCollection<string> permissionNames,
    ResourceDescriptor resource,
    CancellationToken cancellationToken)
  {
    if (permissionNames.Count == 0)
    {
      return new Dictionary<string, PermissionEvaluationResult>();
    }

    var context = await _contextLoader.Load(principal, cancellationToken);
    return permissionNames
      .Distinct(StringComparer.Ordinal)
      .ToFrozenDictionary(
        permissionName => permissionName,
        permissionName => _decisionEvaluator.Evaluate(context, permissionName, resource),
        StringComparer.Ordinal);
  }

  public async Task<IReadOnlySet<string>> GetGrantedPolicies(
    PrincipalDescriptor principal,
    CancellationToken cancellationToken)
  {
    var clientDefinitions = PermissionPolicies.ClientDefinitions;
    if (clientDefinitions.Count == 0)
    {
      return new HashSet<string>(StringComparer.Ordinal);
    }

    var grantedPolicies = new HashSet<string>(StringComparer.Ordinal);
    var tenantEntries = new List<KeyValuePair<string, PermissionPolicyDefinition>>();
    var serverEntries = new List<KeyValuePair<string, PermissionPolicyDefinition>>();

    foreach (var entry in clientDefinitions)
    {
      switch (entry.Value.ResourceScopeKind)
      {
        case PermissionScopeKind.Tenant:
          tenantEntries.Add(entry);
          break;
        case PermissionScopeKind.Server:
          serverEntries.Add(entry);
          break;
        default:
          throw new InvalidOperationException(
            $"Projected client policy '{entry.Key}' has an unsupported resource kind " +
            $"'{entry.Value.ResourceScopeKind}'. Only tenant and server policies can be projected to the client.");
      }
    }

    if (serverEntries.Count > 0)
    {
      var serverResource = _resourceDescriptorFactory.CreateServer();
      await AddGrantedPoliciesCore(
        principal,
        serverEntries,
        serverResource,
        grantedPolicies,
        cancellationToken);
    }

    if (tenantEntries.Count > 0)
    {
      if (!principal.TenantId.HasValue)
      {
        throw new InvalidOperationException(
          "Cannot evaluate tenant client policies for a principal without a tenant id.");
      }

      var tenantResource = _resourceDescriptorFactory.CreateTenant(principal.TenantId.Value);
      await AddGrantedPoliciesCore(
        principal,
        tenantEntries,
        tenantResource,
        grantedPolicies,
        cancellationToken);
    }

    return grantedPolicies.ToFrozenSet();
  }

  private async Task AddGrantedPoliciesCore(
    PrincipalDescriptor principal,
    IReadOnlyCollection<KeyValuePair<string, PermissionPolicyDefinition>> entries,
    ResourceDescriptor resource,
    ISet<string> grantedPolicies,
    CancellationToken cancellationToken)
  {
    var permissionNames = entries
      .Select(entry => entry.Value.PermissionName)
      .Distinct(StringComparer.Ordinal)
      .ToArray();

    if (permissionNames.Length == 0)
    {
      return;
    }

    var decisions = await EvaluateMany(principal, permissionNames, resource, cancellationToken);

    foreach (var (policyName, definition) in entries)
    {
      if (decisions.TryGetValue(definition.PermissionName, out var decision) && decision.Allowed)
      {
        grantedPolicies.Add(policyName);
      }
    }
  }
}
