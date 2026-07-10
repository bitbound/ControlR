using System.Security.Claims;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Services.ServiceAccounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Web.Server.Tests.Helpers;

/// <summary>
/// Shared helpers for constructing server service account principals and controllers in tests.
/// </summary>
internal static class TestPrincipalHelper
{

  /// <summary>
  /// Creates a controller instance with a server service account principal already configured.
  /// </summary>
  public static async Task<T> CreateControllerWithServerServiceAccountAsync<T>(
    IServiceScope scope,
    string? accountName = null,
    CancellationToken cancellationToken = default) where T : ControllerBase
  {
    var (principal, _) = await CreateServerServiceAccountAsync(scope.ServiceProvider, accountName, cancellationToken);
    var controller = scope.CreateController<T>();
    controller.ControllerContext.HttpContext.User = principal;
    return controller;
  }

  /// <summary>
  /// Creates a server service account, resolves it from the database, and returns
  /// a <see cref="ClaimsPrincipal"/> configured with the required claims.
  /// </summary>
  public static async Task<(ClaimsPrincipal Principal, CreateServiceAccountResponseDto Account)> CreateServerServiceAccountAsync(
    IServiceProvider services,
    string? accountName = null,
    CancellationToken cancellationToken = default)
  {
    var manager = services.GetRequiredService<IServiceAccountManager>();
    var account = await manager.CreateServer(accountName ?? $"test-sa-{Guid.NewGuid():N}", null, cancellationToken);

    if (!account.IsSuccess)
    {
      throw new InvalidOperationException($"Failed to create server service account: {account.Reason}");
    }

    return (CreateServerServiceAccountPrincipal(account.Value!), account.Value!);
  }

  /// <summary>
  /// Builds a <see cref="ClaimsPrincipal"/> for a server service account from an existing account.
  /// </summary>
  public static ClaimsPrincipal CreateServerServiceAccountPrincipal(CreateServiceAccountResponseDto account)
  {
    var credential = account.ServiceAccount.Credentials[0];
    return new ClaimsPrincipal(new ClaimsIdentity([
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimTypes.ServerServiceAccount),
      new Claim(PrincipalClaimTypes.PrincipalId, account.ServiceAccount.Id.ToString()),
      new Claim(UserClaimTypes.AuthenticationMethod, PrincipalClaimTypes.ServiceAccountCredentialMethod),
      new Claim(PrincipalClaimTypes.CredentialId, credential.Id.ToString()),
    ], "TestAuth"));
  }
}