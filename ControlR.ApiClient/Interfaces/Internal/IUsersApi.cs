using ControlR.Libraries.Api.Contracts.Constants;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUsersApi
{
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/reset-password", "POST")]
  [Obsolete("Use ControlrApi.V1.Users.AdminResetPassword (POST /api/v1/users/{userId}/reset-password?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<AdminResetPasswordResponseDto>> AdminResetPassword(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}", "POST")]
  [Obsolete("Use ControlrApi.V1.Users.CreateUser (POST /api/v1/users?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<UserResponseDto>> CreateUser(CreateUserRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/personal-access-tokens", "POST")]
  [Obsolete("Use ControlrApi.V1.Users.CreateUserPersonalAccessToken (POST /api/v1/users/{userId}/personal-access-tokens?tenantId=), which requires tenantId and returns 201. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreateUserPersonalAccessToken(Guid userId, CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}", "DELETE")]
  [Obsolete("Use ControlrApi.V1.Users.DeleteUser (DELETE /api/v1/users/{userId}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> DeleteUser(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/personal-access-tokens/{{tokenId}}", "DELETE")]
  [Obsolete("Use ControlrApi.V1.Users.DeleteUserPersonalAccessToken (DELETE /api/v1/users/{userId}/personal-access-tokens/{tokenId}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult> DeleteUserPersonalAccessToken(Guid userId, Guid tokenId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}", "GET")]
  [Obsolete("Use ControlrApi.V1.Users.GetAllUsers (GET /api/v1/users?tenantId=), which requires tenantId and returns an Items envelope. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<UserResponseDto[]>> GetAllUsers(CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/personal-access-tokens", "GET")]
  [Obsolete("Use ControlrApi.V1.Users.GetUserPersonalAccessTokens (GET /api/v1/users/{userId}/personal-access-tokens?tenantId=), which requires tenantId and returns an Items envelope. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<PersonalAccessTokenResponseDto[]>> GetUserPersonalAccessTokens(Guid userId, CancellationToken cancellationToken = default);
  [ApiRoute($"{HttpConstants.Internal.UsersEndpoint}/{{userId}}/personal-access-tokens/{{tokenId}}", "PUT")]
  [Obsolete("Use ControlrApi.V1.Users.UpdateUserPersonalAccessToken (PUT /api/v1/users/{userId}/personal-access-tokens/{tokenId}?tenantId=), which requires tenantId as a query parameter. This internal route is unversioned and slated for removal.")]
  Task<ApiResult<PersonalAccessTokenResponseDto>> UpdateUserPersonalAccessToken(Guid userId, Guid tokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
