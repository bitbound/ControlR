using ControlR.Libraries.Shared.Helpers;
using System.Security.Cryptography;
using System.Text;

namespace ControlR.Web.Server.Services;

public interface IApiKeyManager
{
  Task<Result<CreateApiKeyResponseDto>> CreateKey(CreateApiKeyRequestDto request, Guid tenantId);
  Task<Result> Delete(Guid id);
  Task<IEnumerable<ApiKeyDto>> GetAll();
  Task<Result<ApiKeyDto>> Update(Guid id, UpdateApiKeyRequestDto request);
  /// <summary>
  /// Validates the provided API key and returns the associated tenant ID if valid.
  /// </summary>
  /// <param name="apiKey">The API key to validate.</param>
  /// <returns>The associated tenant ID if valid, otherwise an error.</returns>
  Task<Result<Guid>> ValidateApiKey(string apiKey);
}

public class ApiKeyManager(
  AppDb appDb,
  TimeProvider timeProvider,
  IPasswordHasher<string> passwordHasher) : IApiKeyManager
{
  private readonly AppDb _appDb = appDb;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IPasswordHasher<string> _passwordHasher = passwordHasher;

  public async Task<Result<CreateApiKeyResponseDto>> CreateKey(CreateApiKeyRequestDto request, Guid tenantId)
  {
    try
    {
      var plainTextKey = RandomGenerator.CreateApiKey();
      var hashedKey = _passwordHasher.HashPassword(string.Empty, plainTextKey);

      var apiKey = new ApiKey
      {
        FriendlyName = request.FriendlyName,
        HashedKey = hashedKey,
        TenantId = tenantId
      };

      _appDb.ApiKeys.Add(apiKey);
      await _appDb.SaveChangesAsync();

      var hexKey = Convert.ToHexString(apiKey.Id.ToByteArray());
      var combinedKey = $"{hexKey}:{plainTextKey}";
      var response = new CreateApiKeyResponseDto(MapToDto(apiKey), combinedKey);
      return Result.Ok(response);
    }
    catch (Exception ex)
    {
      return Result.Fail<CreateApiKeyResponseDto>(ex, "Failed to create API key");
    }
  }

  public async Task<Result> Delete(Guid id)
  {
    try
    {
      var apiKey = await _appDb.ApiKeys
        .FirstOrDefaultAsync(x => x.Id == id);

      if (apiKey is null)
      {
        return Result.Fail("API key not found");
      }

      _appDb.ApiKeys.Remove(apiKey);
      await _appDb.SaveChangesAsync();

      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result.Fail(ex, "Failed to delete API key");
    }
  }

  public async Task<IEnumerable<ApiKeyDto>> GetAll()
  {
    var apiKeys = await _appDb.ApiKeys
      .OrderByDescending(x => x.CreatedAt)
      .ToListAsync();

    return apiKeys.Select(MapToDto);
  }

  public async Task<Result<ApiKeyDto>> Update(Guid id, UpdateApiKeyRequestDto request)
  {
    try
    {
      var apiKey = await _appDb.ApiKeys
        .FirstOrDefaultAsync(x => x.Id == id);

      if (apiKey is null)
      {
        return Result.Fail<ApiKeyDto>("API key not found");
      }

      apiKey.FriendlyName = request.FriendlyName;
      await _appDb.SaveChangesAsync();

      return Result.Ok(MapToDto(apiKey));
    }
    catch (Exception ex)
    {
      return Result.Fail<ApiKeyDto>(ex, "Failed to update API key");
    }
  }

  public async Task<Result<Guid>> ValidateApiKey(string apiKey)
  {
    try
    {
      var parts = apiKey.Split(':', 2);
      if (parts.Length != 2)
      {
        return Result.Fail<Guid>("Invalid API key format");
      }

      var keyIdBytes = Convert.FromHexString(parts[0]);
      var apiKeyId = new Guid(keyIdBytes);
      
      var storedKey = await _appDb.ApiKeys
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == apiKeyId);

      if (storedKey is null)
      {
        return Result.Fail<Guid>("Invalid API key");
      }

      var isValid = _passwordHasher.VerifyHashedPassword(string.Empty, storedKey.HashedKey, parts[1]) == PasswordVerificationResult.Success;

      if (!isValid)
      {
        return Result.Fail<Guid>("Invalid API key");
      }

      // Update last used timestamp
      storedKey.LastUsed = _timeProvider.GetUtcNow();
      await _appDb.SaveChangesAsync();

      return Result.Ok(storedKey.TenantId);
    }
    catch (Exception ex)
    {
      return Result.Fail<Guid>(ex, "Failed to validate API key");
    }
  }

  private static ApiKeyDto MapToDto(ApiKey apiKey)
  {
    return new ApiKeyDto(
      apiKey.Id,
      apiKey.FriendlyName,
      apiKey.CreatedAt,
      apiKey.LastUsed);
  }
}
