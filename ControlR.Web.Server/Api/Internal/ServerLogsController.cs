using Microsoft.AspNetCore.Mvc;

namespace ControlR.Web.Server.Api.Internal;

[Route(HttpConstants.Internal.ServerLogsEndpoint)]
[ApiController]
[Authorize(Policy = PolicyNames.RequireServerTelemetryRead)]
[EndpointGroupName(OpenApiConstants.InternalGroupName)]
public class ServerLogsController(
  IWebHostEnvironment webHostEnvironment,
  IOptionsMonitor<AspireDashboardOptions> aspireOptions) : ControllerBase
{
  private readonly IOptionsMonitor<AspireDashboardOptions> _aspireOptions = aspireOptions;
  private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;
  
  [HttpGet("get-aspire-url")]
  [ApiDeprecated("/api/v1/server-logs/get-aspire-url")]
  public async Task<ActionResult<InternalDtos.GetAspireUrlResponseDto>> GetAspireUrl()
  {
    var aspireToken = _aspireOptions.CurrentValue.Token;
    var aspireUrl = _aspireOptions.CurrentValue.PublicWebUrl;

    if (aspireUrl is null || string.IsNullOrWhiteSpace(aspireToken))
    {
      if (_webHostEnvironment.IsDevelopment())
      {
        return Ok(new InternalDtos.GetAspireUrlResponseDto(
          IsConfigured: true, 
          AspireUrl: new Uri("http://localhost:18888")));
      }

      return Ok(new InternalDtos.GetAspireUrlResponseDto(
        IsConfigured: false, 
        AspireUrl: null));
    } 

    var aspireBaseUrl = new Uri(aspireUrl, $"/login?t={Uri.EscapeDataString(aspireToken)}");
    return Ok(new InternalDtos.GetAspireUrlResponseDto(
      IsConfigured: true, 
      AspireUrl: aspireBaseUrl));
  }
}
