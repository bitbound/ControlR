namespace ControlR.Web.Server.OpenApi;

/// <summary>
/// Marks an endpoint as deprecated. Emitting <c>deprecated: true</c> in the OpenAPI document
/// is the entire deprecation mechanism. Removal timing is governed by release notes.
/// Used instead of <c>[Obsolete]</c> because warnings-as-errors would break every caller
/// that consumes the generated client.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ApiDeprecatedAttribute : Attribute
{
  public ApiDeprecatedAttribute(string replacementRoute)
  {
    ReplacementRoute = replacementRoute;
  }

  /// <summary>
  /// Optional migration caveat appended to the description, e.g. when the replacement
  /// request body differs from the deprecated one.
  /// </summary>
  public string? Note { get; init; }

  /// <summary>
  /// Route of the V1 endpoint that replaces this one. Also emitted as the machine-readable
  /// <c>x-replacement-route</c> OpenAPI extension.
  /// </summary>
  public string ReplacementRoute { get; }
}
