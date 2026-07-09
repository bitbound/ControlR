using ControlR.Web.Server.Authn;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class OpenApiSecurityTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
  private const string CookieScheme = "Cookie";
  private const string PatScheme = PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme;
  private const string ServiceAccountScheme = ServiceAccountCredentialAuthenticationSchemeOptions.DefaultScheme;

  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

    document.Components.SecuritySchemes[CookieScheme] = new OpenApiSecurityScheme
    {
      Type = SecuritySchemeType.ApiKey,
      In = ParameterLocation.Cookie,
      Name = ".AspNetCore.Identity.Application",
      Description = "Interactive browser session cookie"
    };

    document.Components.SecuritySchemes[PatScheme] = new OpenApiSecurityScheme
    {
      Type = SecuritySchemeType.ApiKey,
      In = ParameterLocation.Header,
      Name = PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      Description = "Personal access token"
    };

    document.Components.SecuritySchemes[ServiceAccountScheme] = new OpenApiSecurityScheme
    {
      Type = SecuritySchemeType.ApiKey,
      In = ParameterLocation.Header,
      Name = ServiceAccountCredentialAuthenticationSchemeOptions.DefaultHeaderName,
      Description = "Service account API key"
    };

    return Task.CompletedTask;
  }

  public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
  {
    var authorizeData = context.Description.ActionDescriptor.EndpointMetadata
      .OfType<IAuthorizeData>()
      .ToList();

    if (authorizeData.Count == 0)
    {
      return Task.CompletedTask;
    }

    var policyNames = authorizeData
      .Select(ad => ad.Policy)
      .Where(p => !string.IsNullOrEmpty(p))
      .Select(p => p!)
      .ToHashSet();

    if (policyNames.Count == 0)
    {
      return Task.CompletedTask;
    }

    var schemes = ResolveSecuritySchemes(policyNames);
    if (schemes.Count == 0)
    {
      return Task.CompletedTask;
    }

    operation.Security ??= [];
    var requirement = new OpenApiSecurityRequirement();
    foreach (var scheme in schemes)
    {
      requirement[new OpenApiSecuritySchemeReference(scheme, context.Document)] = [];
    }

    operation.Security.Add(requirement);

    return Task.CompletedTask;
  }

  private static HashSet<string> ResolveSecuritySchemes(HashSet<string> policyNames)
  {
    var schemes = new HashSet<string>();

    if (policyNames.Contains(RequireServerServiceAccountPolicy.PolicyName))
    {
      schemes.Add(ServiceAccountScheme);
    }

    if (policyNames.Contains(RequireUserPrincipalPolicy.PolicyName))
    {
      schemes.Add(CookieScheme);
      schemes.Add(PatScheme);
    }

    if (policyNames.Contains(CombinedAuthorizationPolicies.RequireServerOrTenantAdminPolicy) ||
        policyNames.Contains(CombinedAuthorizationPolicies.RequireServerOrTenantAdminOrInstallerKeyManagerPolicy))
    {
      schemes.Add(ServiceAccountScheme);
      schemes.Add(CookieScheme);
      schemes.Add(PatScheme);
    }

    return schemes;
  }
}
