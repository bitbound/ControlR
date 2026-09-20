namespace ControlR.Web.Server.OpenApi;

/// <summary>
/// Marks an action that reads a multipart form off the request itself rather than binding it as a
/// parameter. Bound parameters are read before the action's guards run, so these actions have to read
/// the form themselves and nothing in their signature describes the body. <see cref="FileUploadTransformer" />
/// documents the part names for an action that carries this.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MultipartRequestBodyAttribute : Attribute;
