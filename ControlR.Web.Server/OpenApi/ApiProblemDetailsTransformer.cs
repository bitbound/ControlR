using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class ApiProblemDetailsTransformer : IOpenApiDocumentTransformer
{
  public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    var problemDetailsSchema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), cancellationToken: cancellationToken);
    document.AddComponent("ProblemDetails", problemDetailsSchema);

    foreach (var pathEntry in document.Paths)
    {
      if (!pathEntry.Key.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      if (pathEntry.Value.Operations is not { } operations)
      {
        continue;
      }

      foreach (var operation in operations.Values)
      {
        operation.Responses ??= [];
        if (operation.Responses.ContainsKey("500"))
        {
          continue;
        }

        operation.Responses["500"] = new OpenApiResponse
        {
          Description = "Internal Server Error",
          Content = new Dictionary<string, OpenApiMediaType>
          {
            ["application/problem+json"] = new OpenApiMediaType
            {
              Schema = new OpenApiSchemaReference("ProblemDetails", document)
            }
          }
        };
      }
    }
  }
}
