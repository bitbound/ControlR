using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PersonalAccessTokens;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.Users;

namespace ControlR.ApiClient.Interfaces.V1;

public interface IUsersApi
{
  [ApiRoute($"{HttpConstants.V1.UsersEndpoint}/{{userId}}/reset-password?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<AdminResetPasswordResponseDto>> AdminResetPassword(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UsersEndpoint}?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<UserResponseDto>> CreateUser(Guid tenantId, CreateUserRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UsersEndpoint}/{{userId}}/personal-access-tokens?tenantId={{tenantId}}", "POST")]
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreateUserPersonalAccessToken(Guid userId, Guid tenantId, CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UsersEndpoint}/{{userId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteUser(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UsersEndpoint}/{{userId}}/personal-access-tokens/{{tokenId}}?tenantId={{tenantId}}", "DELETE")]
  Task<ApiResult> DeleteUserPersonalAccessToken(Guid userId, Guid tokenId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UsersEndpoint}?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<UsersResponseDto>> GetAllUsers(Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UsersEndpoint}/{{userId}}/personal-access-tokens?tenantId={{tenantId}}", "GET")]
  Task<ApiResult<PersonalAccessTokenResponseDto[]>> GetUserPersonalAccessTokens(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

  [ApiRoute($"{HttpConstants.V1.UsersEndpoint}/{{userId}}/personal-access-tokens/{{tokenId}}?tenantId={{tenantId}}", "PUT")]
  Task<ApiResult<PersonalAccessTokenResponseDto>> UpdateUserPersonalAccessToken(Guid userId, Guid tokenId, Guid tenantId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
