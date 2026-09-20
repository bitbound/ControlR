using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

/// <summary>
/// Gives the V1 operations that write a raw response body themselves the media type they return. A
/// streamed response has no DTO, and an attribute that declares no type also declares no content type.
/// Each action names its own media type through <see cref="BinaryResponseAttribute" />, so adding a
/// binary endpoint needs no change here.
/// </summary>
public class BinaryResponseTransformer : IOpenApiOperationTransformer
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

    var mediaTypes = actionDescriptor.MethodInfo
      .GetCustomAttributes(inherit: true)
      .OfType<BinaryResponseAttribute>()
      .Select(attribute => attribute.MediaType)
      .ToList();

    if (mediaTypes.Count == 0)
    {
      return Task.CompletedTask;
    }

    if (operation.Responses is not { } responses ||
        !responses.TryGetValue(StatusCodes.Status200OK.ToString(), out var response) ||
        response is not OpenApiResponse binaryResponse)
    {
      return Task.CompletedTask;
    }

    binaryResponse.Content ??= new Dictionary<string, OpenApiMediaType>();
    foreach (var mediaType in mediaTypes)
    {
      // A media type with no schema: the action writes the bytes itself, so the contract states what
      // they are and nothing more.
      binaryResponse.Content[mediaType] = new OpenApiMediaType();
    }

    return Task.CompletedTask;
  }
}
