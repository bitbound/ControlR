using Asp.Versioning;
using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Web.Server.Data.Enums;
using ControlR.Web.Server.Services.LogonTokens;
using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.V0;

[Route(HttpConstants.V0.LogonTokensEndpoint)]
[ApiController]
[Authorize(Policy = RequireServerServiceAccountPolicy.PolicyName)]
[ApiVersion(ApiVersions.V0)]
public class LogonTokensController : ControllerBase
{
  [HttpPost]
  public async Task<ActionResult<V0Dtos.LogonTokenResponseDto>> Create(
    [FromServices] AppDb appDb,
    [FromServices] UserManager<AppUser> userManager,
    [FromServices] ILogonTokenProvider logonTokenProvider,
    [FromBody] V0Dtos.CreateLogonTokenRequestDto request)
  {
    var device = await appDb.Devices.FindAsync(request.DeviceId);
    if (device is null || device.TenantId != request.TenantId)
    {
      return BadRequest("Device not found");
    }

    Guid? userId = request.UserId;

    if (request.Kind == LogonTokenKind.Service)
    {
      if (string.IsNullOrWhiteSpace(request.UserCorrelationId))
      {
        return BadRequest("UserCorrelationId is required for service logon tokens.");
      }

      var username = $"ext-{request.UserCorrelationId.Trim()}";
      var guestUser = await userManager.Users
        .FirstOrDefaultAsync(u => u.UserName == username && u.TenantId == request.TenantId);

      if (guestUser is null)
      {
        guestUser = new AppUser
        {
          UserName = username,
          Email = $"{username}@controlr.local",
          TenantId = request.TenantId,
          AccountType = AccountType.ExternalUser
        };
        var createResult = await userManager.CreateAsync(guestUser);
        if (!createResult.Succeeded)
        {
          return BadRequest("Failed to create guest user.");
        }
      }

      await userManager.UpdateLastLoginAsync(guestUser);

      userId = guestUser.Id;
    }
    else if (request.Kind == LogonTokenKind.User)
    {
      if (!request.UserId.HasValue)
      {
        return BadRequest("UserId is required for user logon tokens.");
      }

      var user = await appDb.Users.FindAsync(request.UserId.Value);
      if (user is null || user.TenantId != request.TenantId)
      {
        return BadRequest("User not found or does not belong to this tenant.");
      }
    }

    var logonToken = await logonTokenProvider.CreateTokenAsync(
      request.DeviceId,
      request.TenantId,
      userId,
      request.Kind,
      request.ExpirationMinutes);

    var deviceAccessUrl = new Uri(
      Request.ToOrigin(),
      $"/device-access?deviceId={request.DeviceId}&logonToken={logonToken.Token}");

    var response = new V0Dtos.LogonTokenResponseDto(
      DeviceAccessUrl: deviceAccessUrl,
      ExpiresAt: logonToken.ExpiresAt,
      Token: logonToken.Token);

    return Ok(response);
  }
}
