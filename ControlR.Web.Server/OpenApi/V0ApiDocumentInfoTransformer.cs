using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class V0ApiDocumentInfoTransformer : IOpenApiDocumentTransformer
{
  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    document.Info.Title = "ControlR | S2S API v0";
    document.Info.Description = "Versioned server-to-server (S2S) API";
    document.Info.Version = "v0";
    return Task.CompletedTask;
  }
}
