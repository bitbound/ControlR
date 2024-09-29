using ControlR.Web.Server.Services.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[OutputCache(Duration = 60)]
[Authorize]
public class ServerSettingsController : ControllerBase
{
  [HttpGet]
  public async Task<ServerSettingsDto> Get(
    [FromServices] IRepository repo,
    [FromServices] IOptionsMonitor<ApplicationOptions> applicationOptions)
  {
    var userCount = await repo.Count<AppUser>();
    var registrationEnabled = userCount == 0 || applicationOptions.CurrentValue.EnablePublicRegistration;
    return new ServerSettingsDto(registrationEnabled);
  }
}
