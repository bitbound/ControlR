using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Server.Services.Users;

public class CreateUserResult(bool succeeded, IdentityResult identityResult, AppUser? user = null)
{
  [MemberNotNullWhen(true, nameof(User))]
  public bool Succeeded { get; init; } = succeeded;

  public IdentityResult IdentityResult { get; init; } = identityResult;
  public AppUser? User { get; init; } = user;
}