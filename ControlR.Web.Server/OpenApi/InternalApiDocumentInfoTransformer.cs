using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class InternalApiDocumentInfoTransformer : IOpenApiDocumentTransformer
{
  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    document.Info.Title = "ControlR | Internal API";
    document.Info.Description = "Dynamically-evolving internal API for first-party clients (BFF for UI, agents, etc.)";
    document.Info.Version = "Dynamic";

    return Task.CompletedTask;
  }
}
