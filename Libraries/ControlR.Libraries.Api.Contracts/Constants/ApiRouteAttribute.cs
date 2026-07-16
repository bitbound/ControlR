namespace ControlR.Libraries.Api.Contracts.Constants;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ApiRouteAttribute(string verb, string routeTemplate) : Attribute
{
  public string RouteTemplate { get; } = routeTemplate;
  public string Verb { get; } = verb;
}
