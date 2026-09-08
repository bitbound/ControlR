namespace ControlR.Libraries.Shared.Services.Http;

public interface IDownloadsApi
{
  Task<Result> DownloadFile(string downloadUri, string destinationPath, CancellationToken cancellationToken = default);
  Task<Result> DownloadFile(Uri downloadUri, string destinationPath, CancellationToken cancellationToken = default);

}

public class DownloadsApi(
    HttpClient client,
    ILogger<DownloadsApi> logger) : IDownloadsApi
{
  private readonly HttpClient _client = client;
  private readonly ILogger<DownloadsApi> _logger = logger;

  public async Task<Result> DownloadFile(string downloadUri, string destinationPath, CancellationToken cancellationToken = default)
  {
    try
    {
      await using var webStream = await _client.GetStreamAsync(downloadUri, cancellationToken);
      await using var fs = new FileStream(destinationPath, FileMode.Create);
      await webStream.CopyToAsync(fs, cancellationToken);
      return Result.Ok();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error while downloading file {DownloadUri}.", downloadUri);
      return Result.Fail(ex);
    }
  }

  public Task<Result> DownloadFile(Uri downloadUri, string destinationPath, CancellationToken cancellationToken = default)
  {
    return DownloadFile($"{downloadUri}", destinationPath, cancellationToken);
  }

}