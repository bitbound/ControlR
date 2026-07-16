using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUsersApi
{
  [ApiRoute("POST", "/api/internal/users/{userId}/reset-password")]
  Task<ApiResult<AdminResetPasswordResponseDto>> AdminResetPassword(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/users")]
  Task<ApiResult<UserResponseDto>> CreateUser(CreateUserRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("POST", "/api/internal/users/{userId}/personal-access-tokens")]
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreateUserPersonalAccessToken(Guid userId, CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/users/{userId}")]
  Task<ApiResult> DeleteUser(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute("DELETE", "/api/internal/users/{userId}/personal-access-tokens/{tokenId}")]
  Task<ApiResult> DeleteUserPersonalAccessToken(Guid userId, Guid tokenId, CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/users")]
  Task<ApiResult<UserResponseDto[]>> GetAllUsers(CancellationToken cancellationToken = default);
  [ApiRoute("GET", "/api/internal/users/{userId}/personal-access-tokens")]
  Task<ApiResult<PersonalAccessTokenResponseDto[]>> GetUserPersonalAccessTokens(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute("PUT", "/api/internal/users/{userId}/personal-access-tokens/{tokenId}")]
  Task<ApiResult<PersonalAccessTokenResponseDto>> UpdateUserPersonalAccessToken(Guid userId, Guid tokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
