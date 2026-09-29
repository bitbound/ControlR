namespace ControlR.Libraries.Api.Contracts.Constants;

public static class DtoLimits
{
  public const int AllowedDesktopSessionIdsMaxCount = 32;
  public const int AuthorizationChangeLogMaxPageSize = 200;
  public const int AuthorizationChangeLogSearchTextMaxLength = 128;
  public const int DeviceIdsMaxCount = 1_000;
  public const int ExpirationMinutesDefault = 15;
  public const int ExpirationMinutesMax = 1440;
  public const int ExpirationMinutesMin = 1;
  public const int PermissionAssignmentIdsMaxCount = 1_000;
  public const int PermissionsMaxLength = 100;
  public const int ServiceAccountDescriptionMaxLength = 500;
  public const int ServiceAccountNameMaxLength = 100;
  public const int ServiceAccountNameMinLength = 1;
  public const int SessionCorrelationIdMaxLength = 128;
  public const int SessionExpirationMinutesDefault = 480;
  public const int SessionExpirationMinutesMax = 1_440;
  public const int SessionExpirationMinutesMin = 5;
  public const int TenantNameMaxLength = 100;
  public const int UserCorrelationIdMaxLength = 252;
  public const int UserDisplayNameMaxLength = 50;
}
