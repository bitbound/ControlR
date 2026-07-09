using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.Middleware;

public class LegacyApiDocumentInfoTransformer : IOpenApiDocumentTransformer
{
  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    document.Info.Title = "ControlR | Legacy API";
    document.Info.Version = "0.1.0";
    document.Info.Description = "ControlR's legacy API.  This API is deprecated and will be removed in a future release.";
    return Task.CompletedTask;
  }
}
