using ControlR.Libraries.Api.Contracts.Constants;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class IdentityApiOpenApiTransformer : IOpenApiDocumentTransformer
{
  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    const string registerPath = $"{HttpConstants.AuthEndpoint}/register";
    if (!document.Paths.TryGetValue(registerPath, out var pathItem))
    {
      return Task.CompletedTask;
    }

    if (pathItem.Operations is not { } operations)
    {
      return Task.CompletedTask;
    }

    foreach (var operation in operations.Values)
    {
      operation.Responses?.TryAdd(
        "404", 
        new OpenApiResponse
        {
          Description = "Not Found"
        });
    }

    return Task.CompletedTask;
  }
}
