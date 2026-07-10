using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Web.Server.Services;

/// <summary>
/// Manages personal access tokens: creation, deletion, retrieval, update, and validation.
/// </summary>
public interface IPersonalAccessTokenManager
{
  /// <summary>
  /// Creates a new personal access token with a randomly generated secret.
  /// Returns the created token and its plaintext secret value.
  /// </summary>
  /// <param name="request">The request containing the token name.</param>
  /// <param name="userId">The ID of the user who owns the token.</param>
  /// <returns>The created token and its plaintext secret, or a failure result.</returns>
  Task<Result<InternalDtos.CreatePersonalAccessTokenResponseDto>> CreateToken(InternalDtos.CreatePersonalAccessTokenRequestDto request, Guid userId);

  /// <summary>
  /// Creates a personal access token with a pre-specified secret and entity ID.
  /// Used for bootstrap scenarios where the token must be known ahead of time.
  /// The caller is responsible for logging the full token string.
  /// </summary>
  /// <param name="tokenId">The GUID to use as the token's ID.</param>
  /// <param name="secret">The plaintext secret to hash and store.</param>
  /// <param name="name">The display name for the token.</param>
  /// <param name="userId">The ID of the user who owns the token.</param>
  /// <returns>The created token, or a failure result.</returns>
  Task<Result<InternalDtos.PersonalAccessTokenDto>> CreateTokenWithKey(Guid tokenId, string secret, string name, Guid userId);

  /// <summary>
  /// Deletes a personal access token.
  /// </summary>
  /// <param name="id">The ID of the token to delete.</param>
  /// <param name="userId">The ID of the user who owns the token.</param>
  /// <returns>A success or failure result.</returns>
  Task<Result> Delete(Guid id, Guid userId);

  /// <summary>
  /// Retrieves all personal access tokens for a user.
  /// </summary>
  /// <param name="userId">The ID of the user whose tokens to retrieve.</param>
  /// <returns>A collection of token DTOs.</returns>
  Task<IEnumerable<InternalDtos.PersonalAccessTokenDto>> GetForUser(Guid userId);

  /// <summary>
  /// Updates a personal access token's name.
  /// </summary>
  /// <param name="id">The ID of the token to update.</param>
  /// <param name="request">The request containing the new name.</param>
  /// <param name="userId">The ID of the user who owns the token.</param>
  /// <returns>The updated token, or a failure result.</returns>
  Task<Result<InternalDtos.PersonalAccessTokenDto>> Update(Guid id, InternalDtos.UpdatePersonalAccessTokenRequestDto request, Guid userId);

  /// <summary>
  /// Validates the provided personal access token and returns the associated user and tenant ID if valid.
  /// </summary>
  /// <param name="token">The personal access token to validate (format: {hex-guid}:{secret}).</param>
  /// <returns>The associated user and tenant ID if valid, otherwise an error.</returns>
  Task<Result<PersonalAccessTokenValidationResult>> ValidateToken(string token);
}

public class PersonalAccessTokenManager(
  AppDb appDb,
  TimeProvider timeProvider,
  IPasswordHasher<string> passwordHasher) : IPersonalAccessTokenManager
{
  private readonly AppDb _appDb = appDb;
  private readonly IPasswordHasher<string> _passwordHasher = passwordHasher;
  private readonly TimeProvider _timeProvider = timeProvider;

  public async Task<Result<InternalDtos.CreatePersonalAccessTokenResponseDto>> CreateToken(InternalDtos.CreatePersonalAccessTokenRequestDto request, Guid userId)
  {
    try
    {
      var plainTextKey = RandomGenerator.CreateApiKey();
      var hashedKey = _passwordHasher.HashPassword(string.Empty, plainTextKey);

      var personalAccessToken = new PersonalAccessToken
      {
        Name = request.Name,
        HashedKey = hashedKey,
        UserId = userId
      };

      _appDb.PersonalAccessTokens.Add(personalAccessToken);
      await _appDb.SaveChangesAsync();

      var hexId = Convert.ToHexString(personalAccessToken.Id.ToByteArray());
      var combinedKey = $"{hexId}:{plainTextKey}";
      var response = new InternalDtos.CreatePersonalAccessTokenResponseDto(MapToDto(personalAccessToken), combinedKey);
      return Result.Ok(response);
    }
    catch (Exception ex)
    {
      return Result.Fail<InternalDtos.CreatePersonalAccessTokenResponseDto>(ex, "Failed to create personal access token.");
    }
  }

  public async Task<Result<InternalDtos.PersonalAccessTokenDto>> CreateTokenWithKey(Guid tokenId, string secret, string name, Guid userId)
  {
    if (tokenId == Guid.Empty)
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenDto>("Token ID cannot be empty.");
    }

    if (string.IsNullOrWhiteSpace(secret))
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenDto>("Secret cannot be empty.");
    }

    if (secret.Length < 32)
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenDto>("PAT secret must be at least 32 characters.");
    }

    try
    {
      var hashedKey = _passwordHasher.HashPassword(string.Empty, secret);
      var personalAccessToken = new PersonalAccessToken
      {
        Id = tokenId,
        Name = name,
        HashedKey = hashedKey,
        UserId = userId
      };

      _appDb.PersonalAccessTokens.Add(personalAccessToken);
      await _appDb.SaveChangesAsync();

      return Result.Ok(MapToDto(personalAccessToken));
    }
    catch (Exception ex)
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenDto>(ex, "Failed to create personal access token with pre-keyed secret.");
    }
  }

  public async Task<Result> Delete(Guid id, Guid userId)
  {
    try
    {
      var personalAccessToken = await _appDb.PersonalAccessTokens
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

      if (personalAccessToken is null)
      {
        return Result.Fail("Personal access token not found.");
      }

      _appDb.PersonalAccessTokens.Remove(personalAccessToken);
      await _appDb.SaveChangesAsync();

      return Result.Ok();
    }
    catch (Exception ex)
    {
      return Result.Fail(ex, "Failed to delete personal access token.");
    }
  }

  public async Task<IEnumerable<InternalDtos.PersonalAccessTokenDto>> GetForUser(Guid userId)
  {
    var personalAccessTokens = await _appDb.PersonalAccessTokens
      .IgnoreQueryFilters()
      .Where(x => x.UserId == userId)
      .OrderByDescending(x => x.CreatedAt)
      .ToListAsync();

    return personalAccessTokens.Select(MapToDto);
  }

  public async Task<Result<InternalDtos.PersonalAccessTokenDto>> Update(Guid id, InternalDtos.UpdatePersonalAccessTokenRequestDto request, Guid userId)
  {
    try
    {
      var personalAccessToken = await _appDb.PersonalAccessTokens
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

      if (personalAccessToken is null)
      {
        return Result.Fail<InternalDtos.PersonalAccessTokenDto>("Personal access token not found.");
      }

      personalAccessToken.Name = request.Name;
      await _appDb.SaveChangesAsync();

      return Result.Ok(MapToDto(personalAccessToken));
    }
    catch (Exception ex)
    {
      return Result.Fail<InternalDtos.PersonalAccessTokenDto>(ex, "Failed to update personal access token.");
    }
  }

  public async Task<Result<PersonalAccessTokenValidationResult>> ValidateToken(string token)
  {
    try
    {
      var parts = token.Split(':', 2);
      if (parts.Length != 2)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Invalid personal access token format.");
      }

      var tokenIdBytes = Convert.FromHexString(parts[0]);
      var tokenId = new Guid(tokenIdBytes);
      
      var storedToken = await _appDb.PersonalAccessTokens
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(x => x.Id == tokenId);

      if (storedToken is null)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Invalid personal access token.");
      }

      var isValid = _passwordHasher.VerifyHashedPassword(string.Empty, storedToken.HashedKey, parts[1]) == PasswordVerificationResult.Success;

      if (!isValid)
      {
        return Result.Fail<PersonalAccessTokenValidationResult>("Invalid personal access token.");
      }

      // Update last used timestamp
      storedToken.LastUsed = _timeProvider.GetUtcNow();
      await _appDb.SaveChangesAsync();

      var result = PersonalAccessTokenValidationResult.Success(storedToken.UserId);
      return Result.Ok(result);
    }
    catch (Exception ex)
    {
      return Result.Fail<PersonalAccessTokenValidationResult>(ex, "Failed to validate personal access token.");
    }
  }

  private static InternalDtos.PersonalAccessTokenDto MapToDto(PersonalAccessToken personalAccessToken)
  {
    return new InternalDtos.PersonalAccessTokenDto(
      personalAccessToken.Id,
      personalAccessToken.Name,
      personalAccessToken.CreatedAt,
      personalAccessToken.LastUsed);
  }
}
