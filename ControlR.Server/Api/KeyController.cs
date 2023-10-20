using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Server.Api;

[Route("api/[controller]")]
[ApiController]
public partial class KeyController : ControllerBase
{
    [HttpGet("verify")]
    [Authorize]
    public IActionResult Verify()
    {
        return Ok();
    }
}