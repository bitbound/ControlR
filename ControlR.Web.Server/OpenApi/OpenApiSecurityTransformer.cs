using ControlR.Web.Server.Authn;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class OpenApiSecurityTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
  private const string AgentSignatureScheme = AgentSignatureAuthenticationSchemeOptions.DefaultScheme;
  private const string CookieScheme = "Cookie";
  private const string InternalDocumentName = "internal";
  private const string PatScheme = PersonalAccessTokenAuthenticationSchemeOptions.DefaultScheme;
  private const string ServiceAccountScheme = ServiceAccountCredentialAuthenticationSchemeOptions.DefaultScheme;
  private const string V1DocumentName = "v1";

  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

    // Only the agent-facing document carries agent operations, and a document must not advertise a
    // credential none of its operations accept.
    if (context.DocumentName == InternalDocumentName)
    {
      document.Components.SecuritySchemes[AgentSignatureScheme] = new OpenApiSecurityScheme
      {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = AgentSignatureAuthenticationSchemeOptions.DefaultHeaderName,
        Description = "Device-signed agent request"
      };
    }

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

    // An agent-only endpoint accepts exactly one credential, the signature its policy names, so the
    // document must not offer the browser cookie or a personal access token for it, both of which
    // would be refused.
    if (authorizeData.Any(data => string.Equals(
      data.Policy,
      AgentSignatureAuthenticationSchemeOptions.DefaultPolicy,
      StringComparison.Ordinal)))
    {
      operation.Security ??= [];
      operation.Security.Add(new OpenApiSecurityRequirement
      {
        [new OpenApiSecuritySchemeReference(AgentSignatureScheme, context.Document)] = []
      });
      return Task.CompletedTask;
    }

    var groupName = context.Description.ActionDescriptor.EndpointMetadata
      .OfType<EndpointGroupNameAttribute>()
      .Select(e => e.EndpointGroupName)
      .FirstOrDefault();

    var schemes = ResolveSecuritySchemes(groupName, context.DocumentName);
    if (schemes.Count == 0)
    {
      return Task.CompletedTask;
    }

    operation.Security ??= [];
    foreach (var scheme in schemes)
    {
      var requirement = new OpenApiSecurityRequirement
      {
        [new OpenApiSecuritySchemeReference(scheme, context.Document)] = []
      };
      operation.Security.Add(requirement);
    }

    return Task.CompletedTask;
  }

  private static HashSet<string> ResolveSecuritySchemes(string? groupName, string documentName)
  {
    var schemes = new HashSet<string>();

    if (groupName == OpenApiConstants.InternalGroupName)
    {
      schemes.Add(CookieScheme);
      schemes.Add(PatScheme);
    }

    if (documentName == V1DocumentName)
    {
      schemes.Add(ServiceAccountScheme);
      schemes.Add(CookieScheme);
      schemes.Add(PatScheme);
    }

    return schemes;
  }
}
