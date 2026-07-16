namespace ControlR.Libraries.Api.Contracts.Constants;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ApiRouteAttribute(string routeTemplate, string verb) : Attribute
{
  public string RouteTemplate { get; } = routeTemplate;
  public string Verb { get; } = verb;
}
