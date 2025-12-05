using ControlR.Libraries.Shared.Constants;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.Middleware;

public class FileUploadTransformer : IOpenApiDocumentTransformer
{
  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    var pathName = $"{HttpConstants.DeviceFileOperationsEndpoint}/upload/{{deviceId}}";
    if (!document.Paths.TryGetValue(pathName, out var uploadPath))
    {
      return Task.CompletedTask;
    }

    if (uploadPath.Operations is not { } operations)
    {
      return Task.CompletedTask;
    }

    foreach (var operation in operations.Values)
    {
      operation.RequestBody = new OpenApiRequestBody
      {
        Content = new Dictionary<string, OpenApiMediaType>
        {
          ["multipart/form-data"] = new OpenApiMediaType
          {
            Schema = new OpenApiSchema
            {
              Type = JsonSchemaType.Object,
              Properties = new Dictionary<string, IOpenApiSchema>
              {
                ["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                ["targetSaveDirectory"] = new OpenApiSchema { Type = JsonSchemaType.String },
                ["overwrite"] = new OpenApiSchema { Type = JsonSchemaType.Boolean }
              },
              Required = new HashSet<string> { "file", "targetSaveDirectory" }
            }
          }
        }
      };
    }
    return Task.CompletedTask;
  }
}
