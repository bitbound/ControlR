using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class ApiDeprecatedOperationTransformer : IOpenApiOperationTransformer
{
  public const string ReplacementRouteExtension = "x-replacement-route";

  public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
  {
    if (context.Description.ActionDescriptor.EndpointMetadata
          .OfType<ApiDeprecatedAttribute>()
          .FirstOrDefault() is not { } attribute)
    {
      return Task.CompletedTask;
    }

    operation.Deprecated = true;

    operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
    operation.Extensions[ReplacementRouteExtension] = new JsonNodeExtension(JsonValue.Create(attribute.ReplacementRoute));

    var message = $"Deprecated. Use `{attribute.ReplacementRoute}` instead.";
    if (!string.IsNullOrWhiteSpace(attribute.Note))
    {
      message += $" {attribute.Note}";
    }

    operation.Description = string.IsNullOrWhiteSpace(operation.Description)
      ? message
      : $"{operation.Description} {message}";

    return Task.CompletedTask;
  }
}
