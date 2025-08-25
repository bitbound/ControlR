using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Web.Server.Services;

public interface IPersonalAccessTokenManager
{
  Task<Result<CreatePersonalAccessTokenResponseDto>> CreateToken(CreatePersonalAccessTokenRequestDto request, Guid tenantId, Guid userId);
  Task<Result> Delete(Guid id, Guid userId);
  Task<IEnumerable<PersonalAccessTokenDto>> GetForUser(Guid userId);
  Task<Result<PersonalAccessTokenDto>> Update(Guid id, UpdatePersonalAccessTokenRequestDto request, Guid userId);
  /// <summary>
  /// Validates the provided personal access token and returns the associated user and tenant ID if valid.
  /// </summary>
  /// <param name="token">The personal access token to validate.</param>
  /// <returns>The associated user and tenant ID if valid, otherwise an error.</returns>
  Task<Result<PersonalAccessTokenValidationResult>> ValidateToken(string token);
}

public class PersonalAccessTokenManager(
  AppDb appDb,
  TimeProvider timeProvider,
  IPasswordHasher<string> passwordHasher) : IPersonalAccessTokenManager
{
  private readonly AppDb _appDb = appDb;
  private readonly TimeProvider _timeProvider = timeProvider;
  private readonly IPasswordHasher<string> _passwordHasher = passwordHasher;

  public async Task<Result<CreatePersonalAccessTokenResponseDto>> CreateToken(CreatePersonalAccessTokenRequestDto request, Guid tenantId, Guid userId)
  {
    try
    {
      var plainTextKey = RandomGenerator.CreateApiKey();
      var hashedKey = _passwordHasher.HashPassword(string.Empty, plainTextKey);

      var personalAccessToken = new PersonalAccessToken
      {
        Name = request.Name,
        HashedKey = hashedKey,
        TenantId = tenantId,
        UserId = userId
      };

      _appDb.PersonalAccessTokens.Add(personalAccessToken);
      await _appDb.SaveChangesAsync();

      var hexKey = Convert.ToHexString(personalAccessToken.Id.ToByteArray());
      var combinedKey = $"{hexKey}:{plainTextKey}";
      var response = new CreatePersonalAccessTokenResponseDto(MapToDto(personalAccessToken), combinedKey);
      return Result.Ok(response);
    }
    catch (Exception ex)
    {
      return Result.Fail<CreatePersonalAccessTokenResponseDto>(ex, "Failed to create personal access token");
    }
  }

  public async Task<Result> Delete(Guid id, Guid userId)
  {
    try
    {
      var personalAccessToken = await _appDb.PersonalAccessTokens
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

      if (personalAccessToken is null)
      {
        return Result.Fail("Personal access token not found");
      }

      _appDb.PersonalAccessTokens.Remove(personalAccessToken);
      await _appDb.SaveChangesAsync();

      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result.Fail(ex, "Failed to delete personal access token");
    }
  }

  public async Task<IEnumerable<PersonalAccessTokenDto>> GetForUser(Guid userId)
  {
    var personalAccessTokens = await _appDb.PersonalAccessTokens
      .Where(x => x.UserId == userId)
      .OrderByDescending(x => x.CreatedAt)
      .ToListAsync();

    return personalAccessTokens.Select(MapToDto);
  }

  public async Task<Result<PersonalAccessTokenDto>> Update(Guid id, UpdatePersonalAccessTokenRequestDto request, Guid userId)
  {
    try
    {
      var personalAccessToken = await _appDb.PersonalAccessTokens
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

      if (personalAccessToken is null)
      {
        return Result.Fail<PersonalAccessTokenDto>("Personal access token not found");
      }

      personalAccessToken.Name = request.Name;
      await _appDb.SaveChangesAsync();

      return Result.Ok(MapToDto(personalAccessToken));
    }
    catch (Exception ex)
    {
      return Result.Fail<PersonalAccessTokenDto>(ex, "Failed to update personal access token");
    }
  }

  public async Task<Result<PersonalAccessTokenValidationResult>> ValidateToken(string token)
  {
    try
    {
      var parts = token.Split(':', 2);
      if (parts.Length != 2)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Invalid personal access token format");
      }

      var tokenIdBytes = Convert.FromHexString(parts[0]);
      var tokenId = new Guid(tokenIdBytes);
      
      var storedToken = await _appDb.PersonalAccessTokens
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == tokenId);

      if (storedToken is null)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Invalid personal access token");
      }

      var isValid = _passwordHasher.VerifyHashedPassword(string.Empty, storedToken.HashedKey, parts[1]) == PasswordVerificationResult.Success;

      if (!isValid)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Invalid personal access token");
      }

      // Update last used timestamp
      storedToken.LastUsed = _timeProvider.GetUtcNow();
      await _appDb.SaveChangesAsync();

      var result = PersonalAccessTokenValidationResult.Success(storedToken.UserId, storedToken.TenantId);
      return Result.Ok(result);
    }
    catch (Exception ex)
    {
      return Result.Fail<PersonalAccessTokenValidationResult>(ex, "Failed to validate personal access token");
    }
  }

  private static PersonalAccessTokenDto MapToDto(PersonalAccessToken personalAccessToken)
  {
    return new PersonalAccessTokenDto(
      personalAccessToken.Id,
      personalAccessToken.Name,
      personalAccessToken.CreatedAt,
      personalAccessToken.LastUsed);
  }
}
