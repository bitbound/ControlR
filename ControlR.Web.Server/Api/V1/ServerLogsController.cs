using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.ServerLogs;

namespace ControlR.Web.Server.Api.V1;

/// <summary>
/// Server log surface. Currently only the Aspire dashboard link.
/// </summary>
[Route(HttpConstants.V1.ServerLogsEndpoint)]
[ApiController]
[Authorize(Policy = PolicyNames.RequireServerTelemetryRead)]
[ApiVersion(ApiVersions.V1)]
public class ServerLogsController(
  IWebHostEnvironment webHostEnvironment,
  IOptionsMonitor<AspireDashboardOptions> aspireOptions) : ControllerBase
{
  private readonly IOptionsMonitor<AspireDashboardOptions> _aspireOptions = aspireOptions;
  private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;

  [HttpGet("get-aspire-url")]
  [ProducesResponseType<GetAspireUrlResponseDto>(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
  public ActionResult<GetAspireUrlResponseDto> GetAspireUrl()
  {
    var aspireToken = _aspireOptions.CurrentValue.Token;
    var aspireUrl = _aspireOptions.CurrentValue.PublicWebUrl;

    if (aspireUrl is null || string.IsNullOrWhiteSpace(aspireToken))
    {
      if (_webHostEnvironment.IsDevelopment())
      {
        return Ok(new GetAspireUrlResponseDto(
          IsConfigured: true,
          AspireUrl: new Uri("http://localhost:18888")));
      }

      return Ok(new GetAspireUrlResponseDto(
        IsConfigured: false,
        AspireUrl: null));
    }

    var aspireBaseUrl = new Uri(aspireUrl, $"/login?t={Uri.EscapeDataString(aspireToken)}");
    return Ok(new GetAspireUrlResponseDto(
      IsConfigured: true,
      AspireUrl: aspireBaseUrl));
  }
}
