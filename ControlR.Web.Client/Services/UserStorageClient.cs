using System.Collections.Concurrent;

namespace ControlR.Web.Client.Services;

public interface IUserStorageClient
{
  Task<string?> GetItem(string key, CancellationToken cancellationToken);
  Task SetItem(string key, string value, CancellationToken cancellationToken);
}

internal class UserStorageClient(
  IControlrApi controlrApi,
  ILogger<UserStorageClient> logger) : IUserStorageClient
{
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<UserStorageClient> _logger = logger;

  private readonly ConcurrentDictionary<string, string?> _storage = new();

  public async Task<string?> GetItem(string key, CancellationToken cancellationToken)
  {
    if (_storage.TryGetValue(key, out var cachedValue))
    {
      return cachedValue;
    }

    var result = await _controlrApi.UserStorage.GetUserStorageItem(key, cancellationToken);
    if (!result.IsSuccess)
    {
      _logger.LogWarning("Failed to get storage key '{Key}'. Reason: {Reason}", key, result.Reason);
      return null;
    }

    var value = result.Value.Value;
    if (value is not null)
    {
      _storage[key] = value;
    }

    return value;
  }

  public async Task SetItem(string key, string value, CancellationToken cancellationToken)
  {
    var response = await _controlrApi.UserStorage.SetUserStorageItem(new(key, value), cancellationToken);
    if (!response.IsSuccess)
    {
      _logger.LogError("Failed to set storage key '{Key}'. Reason: {Reason}", key, response.Reason);
      return;
    }

    var responseValue = response.Value.Value;
    if (responseValue is not null)
    {
      _storage[key] = responseValue;
    }
  }
}
