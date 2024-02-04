using ControlR.Server.Auth;
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

    [HttpGet("verify-administrator")]
    [Authorize(Policy = PolicyNames.RequireAdministratorPolicy)]
    public IActionResult VerifyAdministrator()
    {
        return Ok();
    }
}