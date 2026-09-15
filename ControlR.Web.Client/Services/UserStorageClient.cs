using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components.Authorization;

namespace ControlR.Web.Client.Services;

public interface IUserStorageClient
{
  Task<string?> GetItem(string key, CancellationToken cancellationToken);
  Task SetItem(string key, string value, CancellationToken cancellationToken);
}

internal class UserStorageClient(
  IControlrApi controlrApi,
  AuthenticationStateProvider authState,
  ILogger<UserStorageClient> logger) : IUserStorageClient
{
  private readonly AuthenticationStateProvider _authState = authState;
  private readonly IControlrApi _controlrApi = controlrApi;
  private readonly ILogger<UserStorageClient> _logger = logger;

  private readonly ConcurrentDictionary<string, string?> _storage = new();

  public async Task<string?> GetItem(string key, CancellationToken cancellationToken)
  {
    if (_storage.TryGetValue(key, out var cachedValue))
    {
      return cachedValue;
    }

    if (await GetTenantId() is not { } tenantId)
    {
      _logger.LogWarning("Cannot get storage key '{Key}' - no tenant claim on the signed-in user.", key);
      return null;
    }

    var result = await _controlrApi.V1.UserStorage.GetUserStorageItem(key, tenantId, cancellationToken);
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
    if (await GetTenantId() is not { } tenantId)
    {
      _logger.LogWarning("Cannot set storage key '{Key}' - no tenant claim on the signed-in user.", key);
      return;
    }

    var response = await _controlrApi.V1.UserStorage.SetUserStorageItem(tenantId, new(key, value), cancellationToken);
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

  private async Task<Guid?> GetTenantId()
  {
    var state = await _authState.GetAuthenticationStateAsync();
    return state.User.TryGetTenantId(out var tenantId) ? tenantId : null;
  }
}
