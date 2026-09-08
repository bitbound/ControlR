using System.Security.Claims;
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
    ServiceAccountAccessMode accessMode = ServiceAccountAccessMode.Unrestricted,
    CancellationToken cancellationToken = default) where T : ControllerBase
  {
    var (principal, _, _) = await CreateServerServiceAccountAsync(
      scope.ServiceProvider, accountName, accessMode, cancellationToken);
    var controller = scope.CreateController<T>();
    controller.ControllerContext.HttpContext.User = principal;
    return controller;
  }

  /// <summary>
  /// Creates a server service account, resolves it from the database, and returns
  /// a <see cref="ClaimsPrincipal"/> configured with the required claims.
  /// </summary>
  public static async Task<(ClaimsPrincipal Principal, ServiceAccountResult Account, string PlainTextSecretKey)> CreateServerServiceAccountAsync(
    IServiceProvider services,
    string? accountName = null,
    ServiceAccountAccessMode accessMode = ServiceAccountAccessMode.Unrestricted,
    CancellationToken cancellationToken = default)
  {
    var manager = services.GetRequiredService<IServiceAccountManager>();
    var account = await manager.CreateForServer(accountName ?? $"test-sa-{Guid.NewGuid():N}", null, accessMode, cancellationToken);

    if (!account.IsSuccess)
    {
      throw new InvalidOperationException($"Failed to create server service account: {account.Reason}");
    }

    var credResult = await manager.AddCredentialForServer(
      account.Value.Id, "Test Credential", expiresAt: null, TestActors.ServerServiceAccount(account.Value.Id), cancellationToken);

    if (!credResult.IsSuccess)
    {
      throw new InvalidOperationException($"Failed to create initial credential: {credResult.Reason}");
    }

    return (
      CreateServerServiceAccountPrincipal(account.Value, credResult.Value.Credential),
      account.Value,
      credResult.Value.PlainTextSecretKey);
  }

  /// <summary>
  /// Builds a <see cref="ClaimsPrincipal"/> for a server service account from an existing account.
  /// </summary>
  public static ClaimsPrincipal CreateServerServiceAccountPrincipal(
    ServiceAccountResult account,
    ServiceAccountCredentialResult credential)
  {
    return new ClaimsPrincipal(new ClaimsIdentity([
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.ServerServiceAccount),
      new Claim(PrincipalClaimTypes.PrincipalId, account.Id.ToString()),
      new Claim(UserClaimTypes.AuthenticationMethod, PrincipalClaimValues.ServiceAccountCredentialMethod),
      new Claim(PrincipalClaimTypes.CredentialId, credential.Id.ToString()),
      new Claim(PrincipalClaimTypes.CredentialType, PrincipalClaimValues.ServiceAccountCredentialType),
    ], "TestAuth"));
  }
}