using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.Middleware;

public class OpenApiSchemaTypeTransformer : IOpenApiSchemaTransformer
{
  public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
  {
    if (schema.Type.HasValue &&
        schema.Type.Value.HasFlag(JsonSchemaType.String) &&
        (
          schema.Type.Value.HasFlag(JsonSchemaType.Number) || schema.Type.Value.HasFlag(JsonSchemaType.Integer
        )))
    {
      // Remove the String flag while preserving other flags
      schema.Type = schema.Type.Value & ~JsonSchemaType.String;
    }

    if (schema.Format == "uint32")
    {
      schema.Format = "int32";
    }
    if (schema.Format == "uri")
    {
      schema.Format = null;
    }
    return Task.CompletedTask;
  }
}
