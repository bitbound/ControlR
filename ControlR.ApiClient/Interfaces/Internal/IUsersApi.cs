using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.Internal;

namespace ControlR.ApiClient.Interfaces.Internal;

public interface IUsersApi
{
  Task<ApiResult<AdminResetPasswordResponseDto>> AdminResetPassword(Guid userId, CancellationToken cancellationToken = default);
  Task<ApiResult<UserResponseDto>> CreateUser(CreateUserRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult<CreatePersonalAccessTokenResponseDto>> CreateUserPersonalAccessToken(Guid userId, CreatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteUser(Guid userId, CancellationToken cancellationToken = default);
  Task<ApiResult> DeleteUserPersonalAccessToken(Guid userId, Guid tokenId, CancellationToken cancellationToken = default);
  Task<ApiResult<UserResponseDto[]>> GetAllUsers(CancellationToken cancellationToken = default);
  Task<ApiResult<PersonalAccessTokenResponseDto[]>> GetUserPersonalAccessTokens(Guid userId, CancellationToken cancellationToken = default);
  Task<ApiResult<PersonalAccessTokenResponseDto>> UpdateUserPersonalAccessToken(Guid userId, Guid tokenId, UpdatePersonalAccessTokenRequestDto request, CancellationToken cancellationToken = default);
}
