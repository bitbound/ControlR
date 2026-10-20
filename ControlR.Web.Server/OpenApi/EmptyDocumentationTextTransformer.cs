using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

/// <summary>
/// The XML doc source writes an empty string into the operation summary and parameter
/// descriptions when the action has no doc comment. Those keys carry no information, so drop them.
/// </summary>
public class EmptyDocumentationTextTransformer : IOpenApiDocumentTransformer
{
  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    foreach (var path in document.Paths.Values)
    {
      ClearEmptyDescriptions(path.Parameters);

      if (path.Operations is null)
      {
        continue;
      }

      foreach (var operation in path.Operations.Values)
      {
        if (string.IsNullOrWhiteSpace(operation.Summary))
        {
          operation.Summary = null;
        }

        ClearEmptyDescriptions(operation.Parameters);
      }
    }

    return Task.CompletedTask;
  }

  private static void ClearEmptyDescriptions(IEnumerable<IOpenApiParameter>? parameters)
  {
    if (parameters is null)
    {
      return;
    }

    foreach (var parameter in parameters)
    {
      if (string.IsNullOrWhiteSpace(parameter.Description))
      {
        parameter.Description = null;
      }
    }
  }
}
