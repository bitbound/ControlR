using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Hosting;

namespace ControlR.Web.Client.Services;

public interface IAppEnvironment
{
  bool IsDevelopment();
  bool IsProduction();
}

internal class BrowserAppEnvironment(IWebAssemblyHostEnvironment hostEnvironment) : IAppEnvironment
{
  private readonly IWebAssemblyHostEnvironment _hostEnvironment = hostEnvironment;

  public bool IsDevelopment()
  {
    return _hostEnvironment.IsDevelopment();
  }

  public bool IsProduction()
  {
    return _hostEnvironment.IsProduction();
  }
}

internal class ServerAppEnvironment(IHostEnvironment hostEnvironment) : IAppEnvironment
{
  private readonly IHostEnvironment _hostEnvironment = hostEnvironment;

  public bool IsDevelopment()
  {
    return _hostEnvironment.IsDevelopment();
  }

  public bool IsProduction()
  {
    return _hostEnvironment.IsProduction();
  }
}