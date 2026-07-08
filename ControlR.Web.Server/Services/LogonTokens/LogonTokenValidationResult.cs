using System.Diagnostics.CodeAnalysis;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V0;

namespace ControlR.Web.Server.Services.LogonTokens;

public class LogonTokenValidationResult
{
  public string? DisplayName { get; set; }
  public string? Email { get; set; }
  public string? ErrorMessage { get; set; }
  [MemberNotNullWhen(true, nameof(TenantId))]
  public bool IsValid { get; set; }
  public LogonTokenKind Kind { get; set; }
  public Guid? TenantId { get; set; }

  public Guid? UserId { get; set; }
  public string? UserName { get; set; }

  public static LogonTokenValidationResult Failure(string errorMessage)
  {
    return new LogonTokenValidationResult
    {
      IsValid = false,
      ErrorMessage = errorMessage
    };
  }

  public static LogonTokenValidationResult UserSuccess(
    Guid userId,
    Guid tenantId,
    string? userName,
    string? displayName,
    string? email)
  {
    return new LogonTokenValidationResult
    {
      IsValid = true,
      Kind = LogonTokenKind.User,
      UserId = userId,
      UserName = userName,
      DisplayName = displayName,
      Email = email,
      TenantId = tenantId
    };
  }

  public static LogonTokenValidationResult ServiceSuccess(Guid tenantId)
  {
    return new LogonTokenValidationResult
    {
      IsValid = true,
      Kind = LogonTokenKind.Service,
      TenantId = tenantId
    };
  }
}
