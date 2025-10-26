using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Server.Services.Users;

public class CreateUserResult(bool succeeded, IdentityResult identityResult, AppUser? user = null)
{
  public IdentityResult IdentityResult { get; init; } = identityResult;
  [MemberNotNullWhen(true, nameof(User))]
  public bool Succeeded { get; init; } = succeeded;
  public AppUser? User { get; init; } = user;
}