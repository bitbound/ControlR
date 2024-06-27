using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace ControlR.Server.Api;
[Route("api/[controller]")]
[ApiController]
public class StatusController : ControllerBase
{
    [HttpGet]
    [OutputCache]
    public IActionResult Get()
    {
        return Ok("ok");
    }
}
