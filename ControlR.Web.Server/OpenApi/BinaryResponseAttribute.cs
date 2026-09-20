namespace ControlR.Web.Server.OpenApi;

/// <summary>
/// States the media type of a body the action writes itself. A streamed response has no DTO, so no
/// other part of the endpoint metadata can say what the bytes are.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BinaryResponseAttribute(string mediaType) : Attribute
{
  public string MediaType { get; } = mediaType;
}
