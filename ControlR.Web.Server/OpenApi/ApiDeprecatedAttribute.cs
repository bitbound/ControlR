namespace ControlR.Web.Server.OpenApi;

/// <summary>
/// Marks an endpoint as deprecated, which emits <c>deprecated: true</c> in the OpenAPI document.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ApiDeprecatedAttribute(string replacementRoute) : Attribute
{
  /// <summary>
  /// Optional migration caveat appended to the description, e.g. when the replacement
  /// request body differs from the deprecated one.
  /// </summary>
  public string? Note { get; init; }

  /// <summary>
  /// Route of the V1 endpoint that replaces this one. Also emitted as the machine-readable
  /// <c>x-replacement-route</c> OpenAPI extension.
  /// </summary>
  public string ReplacementRoute { get; } = replacementRoute;
}
