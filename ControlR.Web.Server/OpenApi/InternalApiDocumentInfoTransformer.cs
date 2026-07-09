using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

public class InternalApiDocumentInfoTransformer : IOpenApiDocumentTransformer
{
  private const string InternalPrefix = "Internal";

  public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
  {
    document.Info.Title = "ControlR | Internal API";
    document.Info.Description = "Dynamically-evolving internal API for first-party clients (BFF for UI, agents, etc.)";
    document.Info.Version = "Dynamic";

    if (document.Tags is not null)
    {
      var documentTagsToReplace = document.Tags
        .Where(t => t.Name is not null && t.Name.StartsWith(InternalPrefix, StringComparison.InvariantCultureIgnoreCase))
        .ToList();
        
      foreach (var tag in documentTagsToReplace)
      {
        document.Tags.Remove(tag);
        document.Tags.Add(new OpenApiTag { Name = TrimInternalPrefix(tag.Name) });
      }
    }

    foreach (var pathItem in document.Paths.Values)
    {
      if (pathItem.Operations is null)
      {
        continue;
      }

      foreach (var operation in pathItem.Operations.Values)
      {
        if (operation.Tags is null)
        {
          continue;
        }

        var operationTagsToReplace = operation.Tags
          .Where(t => t.Name is not null && t.Name.StartsWith(InternalPrefix, StringComparison.Ordinal))
          .ToList();
        foreach (var tag in operationTagsToReplace)
        {
          operation.Tags.Remove(tag);
          operation.Tags.Add(new OpenApiTagReference(TrimInternalPrefix(tag.Name)));
        }
      }
    }

    return Task.CompletedTask;
  }

  private static string TrimInternalPrefix(string? name)
  {
    if (name is null)
    {
      return string.Empty;
    }

    return name.StartsWith(InternalPrefix, StringComparison.Ordinal)
      ? name.Substring(InternalPrefix.Length)
      : name;
  }
}
