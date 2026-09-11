namespace ControlR.Web.Server.OpenApi;

/// <summary>
/// Marks an endpoint as deprecated. Emitting <c>deprecated: true</c> in the OpenAPI document
/// is the entire deprecation mechanism; removal timing is governed by release notes.
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
  /// Route of the V1 endpoint that replaces this one, surfaced in the OpenAPI description.
  /// </summary>
  public string ReplacementRoute { get; }
}
