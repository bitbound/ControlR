namespace ControlR.Libraries.Shared.Services.Http;
internal class ServerSettingsApi(
  HttpClient httpClient,
  ILogger<ServerSettingsApi> logger)
{
  private readonly HttpClient _client = httpClient;
  private readonly ILogger<ServerSettingsApi> _logger = logger;
}
