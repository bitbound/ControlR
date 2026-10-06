using ControlR.Web.Server.Services.Authorization;

namespace ControlR.Web.Server.Authz.Permissions;

/// <summary>
/// Authorization handler that bridges ASP.NET Core policy/resource authorization to the
/// centralized <see cref="IPermissionEvaluator"/>. Builds a <see cref="PrincipalDescriptor"/>
/// from the authenticated principal's claims, resolves the resource descriptor, and
/// delegates the allow/deny decision to the evaluator.
/// </summary>
public class PermissionRequirementHandler(
  IPermissionEvaluator evaluator,
  IResourceDescriptorFactory resourceFactory,
  IHttpContextAccessor httpContextAccessor,
  ILogger<PermissionRequirementHandler> logger)
  : AuthorizationHandler<PermissionRequirement, object>
{
  private readonly IPermissionEvaluator _evaluator = evaluator;
  private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
  private readonly ILogger<PermissionRequirementHandler> _logger = logger;
  private readonly IResourceDescriptorFactory _resourceFactory = resourceFactory;

  protected override async Task HandleRequirementAsync(
    AuthorizationHandlerContext context,
    PermissionRequirement requirement,
    object resource)
  {
    var principal = context.User.ToPrincipalDescriptor();
    if (principal is null)
    {
      _logger.LogWarning("Cannot build principal descriptor from claims. Denying {Permissions}.", string.Join(", ", requirement.PermissionNames));
      context.Fail(new AuthorizationFailureReason(this, "Missing required principal claims."));
      return;
    }

    var cancellationToken = _httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;

    try
    {
      var resourceDescriptor = await ResolveResource(requirement.Resource, resource, principal, cancellationToken);
      if (resourceDescriptor is null)
      {
        _logger.LogWarning(
          "Could not resolve resource for {Permissions}. Denying.", string.Join(", ", requirement.PermissionNames));
        context.Fail(new AuthorizationFailureReason(this, "Could not resolve the authorization resource."));
        return;
      }

      var decisions = await _evaluator.EvaluateMany(
        principal, requirement.PermissionNames, resourceDescriptor, cancellationToken);

      if (decisions.Values.Any(x => x.Allowed))
      {
        context.Succeed(requirement);
        return;
      }

      var denialReason = decisions.Values
        .Select(x => x.DenialReason)
        .FirstOrDefault(reason => reason is not null) ?? "Permission denied.";

      _logger.LogDebug(
        "Permission denied: {Permissions} on {Resource} for principal {PrincipalId}. Reason: {Reason}",
        string.Join(", ", requirement.PermissionNames), resourceDescriptor, principal.PrincipalId, denialReason);
      context.Fail(new AuthorizationFailureReason(this, denialReason));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      // A client disconnect / aborted request is not an evaluation failure. Let it propagate so it
      // is not logged as an Error or reported as a generic authorization denial.
      throw;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Authorization evaluation failed for {Permissions}. Denying.", string.Join(", ", requirement.PermissionNames));
      context.Fail(new AuthorizationFailureReason(this, "Authorization evaluation failed."));
    }
  }

  private async Task<ResourceDescriptor?> ResolveResource(
    ResourceDescriptor requirementResource,
    object resource,
    PrincipalDescriptor principal,
    CancellationToken cancellationToken)
  {
    if (resource is ResourceDescriptor resourceDescriptor)
    {
      return resourceDescriptor;
    }

    if (resource is Device device)
    {
      return await _resourceFactory.CreateDevice(device, cancellationToken);
    }

    if (requirementResource.Kind == PermissionScopeKind.Tenant &&
        requirementResource.TenantId is null &&
        principal.TenantId.HasValue)
    {
      return _resourceFactory.CreateTenant(principal.TenantId.Value);
    }

    // Resolve non-Device scope kinds through the factory so the evaluator sees a fully
    // populated descriptor (with Id/TenantId) instead of a bare kind-only descriptor that
    // matches nothing. Returns null (fail closed) when the scope cannot be resolved.
    if (requirementResource.Kind is PermissionScopeKind.DeviceGroup or
        PermissionScopeKind.CustomerTenant or
        PermissionScopeKind.UserGroup or
        PermissionScopeKind.Device)
    {
      if (principal.TenantId is not { } tenantId)
      {
        return null;
      }

      return await _resourceFactory.CreateScope(
        requirementResource.Kind, requirementResource.Id, tenantId, cancellationToken);
    }

    return requirementResource;
  }
}
