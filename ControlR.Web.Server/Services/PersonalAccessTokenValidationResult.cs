using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Server.Services;

public class PersonalAccessTokenValidationResult
{
  [MemberNotNullWhen(true, nameof(UserId), nameof(TenantId))]
  public bool IsValid { get; set; }
  public string? ErrorMessage { get; set; }

  public Guid? UserId { get; set; }
  public Guid? TenantId { get; set; }

  public static PersonalAccessTokenValidationResult Success(Guid userId, Guid tenantId)
  {
    return new PersonalAccessTokenValidationResult
    {
      IsValid = true,
      UserId = userId,
      TenantId = tenantId
    };
  }

  public static PersonalAccessTokenValidationResult Failure(string errorMessage)
  {
    return new PersonalAccessTokenValidationResult
    {
      IsValid = false,
      ErrorMessage = errorMessage
    };
  }
}
