using ControlR.Libraries.Shared.Dtos.ServerApi;
using ControlR.Web.Server.Components.Account;
using ControlR.Web.Server.Services.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[OutputCache(Duration = 60)]
public class ServerSettingsController : ControllerBase
{
  public async Task<ServerSettings> Get(
    [FromServices] IRepository repo,
    [FromServices] IOptionsMonitor<ApplicationOptions> applicationOptions)
  {
    var userCount = await repo.Count<AppUser>();
    var registrationEnabled = userCount == 0 || applicationOptions.CurrentValue.EnablePublicRegistration;
    return new ServerSettings(registrationEnabled);
  }
}
