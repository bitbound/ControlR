using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Adds <see cref="TenantIdRequiredFilter"/> to every V1 action that takes a <c>tenantId</c> GUID,
/// and declares the 400 it can return so the published document matches the behavior. Scoped to the
/// V1 controllers deliberately, because the deprecated internal endpoints keep their shipped 403.
/// </summary>
public sealed class RequireTenantIdActionConvention : IActionModelConvention
{
  private const string TenantIdParameterName = "tenantId";
  private const string V1NamespacePrefix = "ControlR.Web.Server.Api.V1";

  public void Apply(ActionModel action)
  {
    if (action.Controller.ControllerType.Namespace?.StartsWith(V1NamespacePrefix, StringComparison.Ordinal) != true)
    {
      return;
    }

    var takesTenantId = action.Parameters.Any(x =>
      x.Name == TenantIdParameterName && x.ParameterType == typeof(Guid));

    if (!takesTenantId)
    {
      return;
    }

    action.Filters.Add(new TenantIdRequiredFilter(TenantIdParameterName));

    // Declared as filter metadata so the API explorer advertises it.
    action.Filters.Add(new ProducesResponseTypeAttribute(
      typeof(ProblemDetails),
      StatusCodes.Status400BadRequest));
  }
}
