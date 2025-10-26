namespace ControlR.Web.Server.Startup;

// ReSharper disable once ClassNeverInstantiated.Global
public class IdentityForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
  private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    var httpContext = _httpContextAccessor.HttpContext;
    
    // Only forward the cookie if we are on the server and have a context.
    if (httpContext?.User.Identity?.IsAuthenticated != true)
    {
      return base.SendAsync(request, cancellationToken);
    }

    // Check if the request targets the same origin. We don't want to leak the cookie to other sites.
    if (request.RequestUri?.Host != httpContext.Request.Host.Host)
    {
      return base.SendAsync(request, cancellationToken);
    }

    var authCookie = httpContext.Request.Cookies[".AspNetCore.Identity.Application"];
    if (authCookie != null && !request.Headers.Contains("Cookie"))
    {
      request.Headers.Add("Cookie", $".AspNetCore.Identity.Application={authCookie}");
    }

    return base.SendAsync(request, cancellationToken);
  }
}