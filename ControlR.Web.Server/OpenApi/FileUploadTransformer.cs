using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

/// <summary>
/// Gives the upload operations the multipart request body their actions cannot describe, because the
/// actions read the form themselves rather than binding it as a parameter. An action opts in with
/// <see cref="MultipartRequestBodyAttribute" /> instead of being named here, so the list cannot drift
/// out of date.
/// </summary>
public class FileUploadTransformer : IOpenApiOperationTransformer
{
  public Task TransformAsync(
    OpenApiOperation operation,
    OpenApiOperationTransformerContext context,
    CancellationToken cancellationToken)
  {
    if (context.Description.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
    {
      return Task.CompletedTask;
    }

    var hasAttribute = actionDescriptor.MethodInfo
      .GetCustomAttributes(inherit: true)
      .OfType<MultipartRequestBodyAttribute>()
      .Any();

    if (!hasAttribute)
    {
      return Task.CompletedTask;
    }

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

    return Task.CompletedTask;
  }
}
