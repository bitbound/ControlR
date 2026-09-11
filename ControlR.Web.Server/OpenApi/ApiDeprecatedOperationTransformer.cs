using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class ApiDeprecatedOperationTransformer : IOpenApiOperationTransformer
{
  public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
  {
    if (context.Description.ActionDescriptor.EndpointMetadata
          .OfType<ApiDeprecatedAttribute>()
          .FirstOrDefault() is not { } attribute)
    {
      return Task.CompletedTask;
    }

    operation.Deprecated = true;
    operation.Description = string.IsNullOrWhiteSpace(operation.Description)
      ? $"Deprecated. Use <c>{attribute.ReplacementRoute}</c> instead."
      : $"{operation.Description} Deprecated. Use <c>{attribute.ReplacementRoute}</c> instead.";

    return Task.CompletedTask;
  }
}
