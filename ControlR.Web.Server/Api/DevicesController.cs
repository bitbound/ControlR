using ControlR.Web.Client.Extensions;
using ControlR.Web.Server.Services.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Web.Server.Api;

[Route("api/[controller]")]
[ApiController]
[OutputCache(Duration = 60)]
[Authorize]
public class DevicesController : ControllerBase
{

  [HttpGet]
  public async Task<List<DeviceDto>> Get(
    [FromServices] IRepository repo)
  {
    var tenantId = User.IsAuthenticated
    var devices = await repo.GetAll<Device>();
    return devices
      .Select(x => x.ToDto())
      .ToList();
  }
}
