using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUsersApi
{
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/reset-password", "POST")]
  Task<ApiResult<AdminResetPasswordResponseDto>> AdminResetPassword(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}", "POST")]
  Task<ApiResult<UserResponseDto>> CreateUser(CreateUserRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/personal-access-tokens", "POST")]
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreateUserPersonalAccessToken(Guid userId, CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}", "DELETE")]
  Task<ApiResult> DeleteUser(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/personal-access-tokens/{{tokenId}}", "DELETE")]
  Task<ApiResult> DeleteUserPersonalAccessToken(Guid userId, Guid tokenId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}", "GET")]
  Task<ApiResult<UserResponseDto[]>> GetAllUsers(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/personal-access-tokens", "GET")]
  Task<ApiResult<PersonalAccessTokenResponseDto[]>> GetUserPersonalAccessTokens(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/personal-access-tokens/{{tokenId}}", "PUT")]
  Task<ApiResult<PersonalAccessTokenResponseDto>> UpdateUserPersonalAccessToken(Guid userId, Guid tokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
