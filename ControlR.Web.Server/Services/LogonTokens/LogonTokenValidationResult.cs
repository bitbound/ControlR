using System.Diagnostics.CodeAnalysis;

namespace ControlR.Web.Server.Services.LogonTokens;

public class LogonTokenValidationResult
{
  [MemberNotNullWhen(true, nameof(UserId), nameof(UserName), nameof(TenantId))]
  public bool IsValid { get; set; }
  public string? ErrorMessage { get; set; }

  public Guid? UserId { get; set; }
  public string? UserName { get; set; }
  public string? DisplayName { get; set; }
  public string? Email { get; set; }
  public Guid? TenantId { get; set; }

  public static LogonTokenValidationResult Success(
    Guid userId, 
    Guid tenantId,
    string? userName, 
    string? displayName, 
    string? email)
  {
    return new LogonTokenValidationResult
    {
      IsValid = true,
      UserId = userId,
      UserName = userName,
      DisplayName = displayName,
      Email = email,
      TenantId = tenantId
    };
  }

  public static LogonTokenValidationResult Failure(string errorMessage)
  {
    return new LogonTokenValidationResult
    {
      IsValid = false,
      ErrorMessage = errorMessage
    };
  }
}
