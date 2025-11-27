using System.Security.Claims;
using System.Text.Json;
using ControlR.Web.Server.Components.Account.Pages;
using ControlR.Web.Server.Components.Account.Pages.Manage;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace ControlR.Web.Server.Components.Account;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
  // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
  public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
  {
    ArgumentNullException.ThrowIfNull(endpoints);

    var accountGroup = endpoints.MapGroup("/Account");
    var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

    var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

    accountGroup.MapPost("/PerformExternalLogin", (
      HttpContext context,
      [FromServices] SignInManager<AppUser> signInManager,
      [FromForm] string provider,
      [FromForm] string returnUrl) =>
    {
      IEnumerable<KeyValuePair<string, StringValues>> query =
      [
        new("ReturnUrl", returnUrl),
        new("Action", ExternalLogin.LoginCallbackAction)
      ];

      var redirectUrl = UriHelper.BuildRelative(
        context.Request.PathBase,
        "/Account/ExternalLogin",
        QueryString.Create(query));

      var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
      return TypedResults.Challenge(properties, [provider]);
    });

    accountGroup.MapPost("/Logout", async (
      ClaimsPrincipal _,
      SignInManager<AppUser> signInManager,
      [FromForm] string returnUrl) =>
    {
      await signInManager.SignOutAsync();
      return TypedResults.LocalRedirect($"~/{returnUrl}");
    });

    manageGroup.MapPost("/LinkExternalLogin", async (
      HttpContext context,
      [FromServices] SignInManager<AppUser> signInManager,
      [FromForm] string provider) =>
    {
      // Clear the existing external cookie to ensure a clean login process
      await context.SignOutAsync(IdentityConstants.ExternalScheme);

      var redirectUrl = UriHelper.BuildRelative(
        context.Request.PathBase,
        "/Account/Manage/ExternalLogins",
        QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

      var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl,
        signInManager.UserManager.GetUserId(context.User));
      return TypedResults.Challenge(properties, [provider]);
    });

    manageGroup.MapPost("/DownloadPersonalData", async (
      HttpContext context,
      [FromServices] UserManager<AppUser> userManager,
      [FromServices] AuthenticationStateProvider authenticationStateProvider) =>
    {
      var user = await userManager.GetUserAsync(context.User);
      if (user is null)
      {
        return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
      }

      var userId = await userManager.GetUserIdAsync(user);
      downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

      // Only include personal data for download
      var personalData = new Dictionary<string, string>();
      var personalDataProps = typeof(AppUser).GetProperties().Where(
        prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
      foreach (var p in personalDataProps)
      {
        personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
      }

      var logins = await userManager.GetLoginsAsync(user);
      foreach (var l in logins)
      {
        personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
      }

      personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);

      var passkeys = await userManager.GetPasskeysAsync(user);
      var passkeyIndex = 0;
      foreach (var passkey in passkeys)
      {
        personalData.Add($"Passkey[{passkeyIndex++}]", passkey.Name ?? "unnamed");
      }

      var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

      context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
      return TypedResults.File(fileBytes, "application/json", "PersonalData.json");
    });

    accountGroup.MapPost("/PasskeyCreationOptions", async (
      HttpContext context,
      [FromServices] UserManager<AppUser> userManager,
      [FromServices] SignInManager<AppUser> signInManager,
      [FromServices] IAntiforgery antiforgery) =>
    {
      await antiforgery.ValidateRequestAsync(context);

      var user = await userManager.GetUserAsync(context.User);
      if (user is null)
      {
        return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
      }

      var userId = await userManager.GetUserIdAsync(user);
      var userName = await userManager.GetUserNameAsync(user) ?? "User";
      var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
      {
        Id = userId,
        Name = userName,
        DisplayName = userName
      });
      return TypedResults.Content(optionsJson, contentType: "application/json");
    });

    accountGroup.MapPost("/PasskeyRequestOptions", async (
      HttpContext context,
      [FromServices] UserManager<AppUser> userManager,
      [FromServices] SignInManager<AppUser> signInManager,
      [FromServices] IAntiforgery antiforgery,
      [FromQuery] string? username) =>
    {
      await antiforgery.ValidateRequestAsync(context);

      var user = string.IsNullOrEmpty(username) ? null : await userManager.FindByNameAsync(username);
      var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
      return TypedResults.Content(optionsJson, contentType: "application/json");
    });

    return accountGroup;
  }
}