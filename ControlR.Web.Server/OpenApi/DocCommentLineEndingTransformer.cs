using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ControlR.Web.Server.OpenApi;

/// <summary>
/// Writes documentation text with LF so one API surface produces the same document bytes on any build
/// host. The generator re-joins doc comment lines with the host's Environment.NewLine, so a document
/// built on Windows differs from one built on Linux. That fails the CI freshness gate, and it dirties
/// both committed documents after an ordinary build that changed no API surface.
/// </summary>
/// <remarks>
/// Objects that hold a reference are left alone. Their documentation getters fall back to the resolved
/// target, so assigning the value back stores a local copy even when the text did not change, and the
/// writer then emits the target's text at every reference site. Those sites carry no documentation of
/// their own, so there is no line ending here for this transformer to fix.
/// </remarks>
public sealed class DocCommentLineEndingTransformer : IOpenApiDocumentTransformer
{
  public Task TransformAsync(
    OpenApiDocument document,
    OpenApiDocumentTransformerContext context,
    CancellationToken cancellationToken)
  {
    Normalize(document.Info);
    Normalize(document.Tags);
    Normalize(document.Servers);
    Normalize(document.Paths?.Values);
    Normalize(document.Components);

    return Task.CompletedTask;
  }

  private static string? Normalize(string? text) => text?.Replace("\r\n", "\n").Replace('\r', '\n');

  private static void Normalize(OpenApiInfo? info)
  {
    if (info is null)
    {
      return;
    }

    info.Title = Normalize(info.Title);
    info.Summary = Normalize(info.Summary);
    info.Description = Normalize(info.Description);
  }

  private static void Normalize(OpenApiComponents? components)
  {
    if (components is null)
    {
      return;
    }

    Normalize(components.Schemas?.Values);
    Normalize(components.Responses?.Values);
    Normalize(components.Parameters?.Values);
    Normalize(components.Examples?.Values);
    Normalize(components.RequestBodies?.Values);
    Normalize(components.Headers?.Values);
    Normalize(components.SecuritySchemes?.Values);
    Normalize(components.Links?.Values);
    Normalize(components.Callbacks?.Values);
  }

  private static void Normalize(IEnumerable<OpenApiTag>? tags)
  {
    foreach (var tag in tags ?? [])
    {
      tag.Description = Normalize(tag.Description);
      Normalize(tag.ExternalDocs);
    }
  }

  private static void Normalize(IEnumerable<OpenApiServer>? servers)
  {
    foreach (var server in servers ?? [])
    {
      server.Description = Normalize(server.Description);

      foreach (var variable in server.Variables?.Values ?? [])
      {
        variable.Description = Normalize(variable.Description);
      }
    }
  }

  private static void Normalize(IEnumerable<IOpenApiPathItem>? pathItems)
  {
    foreach (var pathItem in pathItems ?? [])
    {
      if (pathItem is IOpenApiReferenceHolder)
      {
        continue;
      }

      // A path item carries documentation about the path itself, separate from its operations.
      pathItem.Summary = Normalize(pathItem.Summary);
      pathItem.Description = Normalize(pathItem.Description);
      Normalize(pathItem.Parameters);
      Normalize(pathItem.Operations?.Values);
    }
  }

  private static void Normalize(IEnumerable<OpenApiOperation>? operations)
  {
    foreach (var operation in operations ?? [])
    {
      operation.Summary = Normalize(operation.Summary);
      operation.Description = Normalize(operation.Description);
      Normalize(operation.Parameters);
      Normalize(operation.RequestBody);
      Normalize(operation.Responses?.Values);

      // An operation's tags are OpenApiTagReference, whose Description has no setter. The document-level
      // tags carry the writable text and are normalized from TransformAsync.
    }
  }

  private static void Normalize(IEnumerable<IOpenApiParameter>? parameters)
  {
    foreach (var parameter in parameters ?? [])
    {
      if (parameter is IOpenApiReferenceHolder)
      {
        continue;
      }

      parameter.Description = Normalize(parameter.Description);
      Normalize(parameter.Content?.Values);
      Normalize(parameter.Schema);
    }
  }

  private static void Normalize(IEnumerable<IOpenApiResponse>? responses)
  {
    foreach (var response in responses ?? [])
    {
      if (response is IOpenApiReferenceHolder)
      {
        continue;
      }

      response.Description = Normalize(response.Description);
      Normalize(response.Content?.Values);
      Normalize(response.Headers?.Values);
      Normalize(response.Links?.Values);
    }
  }

  private static void Normalize(IEnumerable<IOpenApiRequestBody>? requestBodies)
  {
    foreach (var requestBody in requestBodies ?? [])
    {
      Normalize(requestBody);
    }
  }

  private static void Normalize(IOpenApiRequestBody? requestBody)
  {
    if (requestBody is null or IOpenApiReferenceHolder)
    {
      return;
    }

    requestBody.Description = Normalize(requestBody.Description);
    Normalize(requestBody.Content?.Values);
  }

  private static void Normalize(IEnumerable<IOpenApiHeader>? headers)
  {
    foreach (var header in headers ?? [])
    {
      if (header is IOpenApiReferenceHolder)
      {
        continue;
      }

      header.Description = Normalize(header.Description);
      Normalize(header.Schema);
    }
  }

  private static void Normalize(IEnumerable<OpenApiMediaType>? mediaTypes)
  {
    foreach (var mediaType in mediaTypes ?? [])
    {
      Normalize(mediaType.Schema);
    }
  }

  private static void Normalize(IEnumerable<IOpenApiSchema>? schemas)
  {
    foreach (var schema in schemas ?? [])
    {
      Normalize(schema);
    }
  }

  private static void Normalize(IOpenApiSchema? schema)
  {
    if (schema is null or IOpenApiReferenceHolder)
    {
      return;
    }

    schema.Description = Normalize(schema.Description);

    // Title is writable only on the concrete schema, while the interface exposes it read-only.
    if (schema is OpenApiSchema mutableSchema)
    {
      mutableSchema.Title = Normalize(mutableSchema.Title);
    }

    Normalize(schema.Items);
    Normalize(schema.Properties?.Values);
    Normalize(schema.AdditionalProperties);
    Normalize(schema.PatternProperties?.Values);
    Normalize(schema.OneOf);
    Normalize(schema.AnyOf);
    Normalize(schema.AllOf);
    Normalize(schema.Not);
  }

  private static void Normalize(IEnumerable<IOpenApiExample>? examples)
  {
    foreach (var example in examples ?? [])
    {
      if (example is IOpenApiReferenceHolder)
      {
        continue;
      }

      example.Summary = Normalize(example.Summary);
      example.Description = Normalize(example.Description);
    }
  }

  private static void Normalize(IEnumerable<IOpenApiLink>? links)
  {
    foreach (var link in links ?? [])
    {
      if (link is IOpenApiReferenceHolder)
      {
        continue;
      }

      link.Description = Normalize(link.Description);
    }
  }

  private static void Normalize(IEnumerable<IOpenApiSecurityScheme>? securitySchemes)
  {
    foreach (var securityScheme in securitySchemes ?? [])
    {
      if (securityScheme is IOpenApiReferenceHolder)
      {
        continue;
      }

      securityScheme.Description = Normalize(securityScheme.Description);
    }
  }

  private static void Normalize(IEnumerable<IOpenApiCallback>? callbacks)
  {
    foreach (var callback in callbacks ?? [])
    {
      if (callback is IOpenApiReferenceHolder)
      {
        continue;
      }

      Normalize(callback.PathItems?.Values);
    }
  }

  private static void Normalize(OpenApiExternalDocs? externalDocs)
  {
    if (externalDocs is null)
    {
      return;
    }

    externalDocs.Description = Normalize(externalDocs.Description);
  }
}
