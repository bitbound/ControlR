using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class LegacyApiDocumentInfoTransformer : IOpenApiDocumentTransformer
{
  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    document.Info.Title = "ControlR | Legacy API";
    document.Info.Description = "ControlR's legacy API.  This API is deprecated and will be removed in a future release.";
    document.Info.Version = "Legacy";
    return Task.CompletedTask;
  }
}
