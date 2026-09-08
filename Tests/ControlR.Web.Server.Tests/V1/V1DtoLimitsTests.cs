using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Options;

namespace ControlR.Web.Server.Tests.V1;

public class V1DtoLimitsTests
{
  [Fact]
  public void CreateLogonTokenForExternalRequestDto_PermissionsAtLimit_IsValid()
  {
    var values = Enumerable.Repeat("permission.foo", DtoLimits.PermissionsMaxLength).ToArray();

    var dto = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: Guid.NewGuid(),
      TenantId: Guid.NewGuid(),
      UserCorrelationId: "corr-123",
      Permissions: values);

    var results = new List<ValidationResult>();
    var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

    Assert.True(isValid);
  }

  [Fact]
  public void CreateLogonTokenForExternalRequestDto_PermissionsOverLimit_IsNotValid()
  {
    var values = Enumerable.Repeat("permission.foo", DtoLimits.PermissionsMaxLength + 1).ToArray();

    var dto = new V1Dtos.CreateLogonTokenForExternalRequestDto(
      DeviceId: Guid.NewGuid(),
      TenantId: Guid.NewGuid(),
      UserCorrelationId: "corr-123",
      Permissions: values);

    var results = new List<ValidationResult>();
    var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

    Assert.False(isValid);
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(V1Dtos.CreateLogonTokenForExternalRequestDto.Permissions)));
  }

  [Fact]
  public void CreateLogonTokenForUserRequestDto_PermissionsAtLimit_IsValid()
  {
    var values = Enumerable.Repeat("permission.foo", DtoLimits.PermissionsMaxLength).ToArray();

    var dto = new V1Dtos.CreateLogonTokenForUserRequestDto(
      DeviceId: Guid.NewGuid(),
      TenantId: Guid.NewGuid(),
      UserId: Guid.NewGuid(),
      Permissions: values);

    var results = new List<ValidationResult>();
    var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

    Assert.True(isValid);
  }

  [Fact]
  public void CreateLogonTokenForUserRequestDto_PermissionsOverLimit_IsNotValid()
  {
    var values = Enumerable.Repeat("permission.foo", DtoLimits.PermissionsMaxLength + 1).ToArray();

    var dto = new V1Dtos.CreateLogonTokenForUserRequestDto(
      DeviceId: Guid.NewGuid(),
      TenantId: Guid.NewGuid(),
      UserId: Guid.NewGuid(),
      Permissions: values);

    var results = new List<ValidationResult>();
    var isValid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

    Assert.False(isValid);
    Assert.Contains(results, r => r.MemberNames.Contains(nameof(V1Dtos.CreateLogonTokenForUserRequestDto.Permissions)));
  }

  [Fact]
  public void LogonTokenGrantCleanupCutoff_Exceeds_MaxTokenLifetime()
  {
    // Orphaned-grant cleanup must not remove grants while a token (or the cookie session that
    // outlives it) can still be active, so the default cutoff must comfortably exceed the maximum
    // token lifetime. This guards the invariant documented on AppOptions.LogonTokenGrantCleanupAfterDays.
    var cutoffDays = new AppOptions().LogonTokenGrantCleanupAfterDays;
    var maxLifetimeDays = (double)DtoLimits.ExpirationMinutesMax / 1440;
    Assert.True(
      cutoffDays >= maxLifetimeDays * 2,
      $"Default grant cleanup cutoff ({cutoffDays} days) must comfortably exceed the max logon token lifetime ({maxLifetimeDays} days). " +
      $"Raise AppOptions.{nameof(AppOptions.LogonTokenGrantCleanupAfterDays)} or lower {nameof(DtoLimits.ExpirationMinutesMax)}.");
  }

  [Fact]
  public void LogonTokenPermissionsLimit_IsAtLeast_CatalogPermissionCount()
  {
    // Ensure that the logon token request limit is at least as large as the number of permissions in the catalog.
    Assert.True(
      PermissionCatalog.All.Count <= DtoLimits.PermissionsMaxLength,
      $"PermissionCatalog has {PermissionCatalog.All.Count} permissions, exceeding the logon token request limit of {DtoLimits.PermissionsMaxLength}. Increase {nameof(DtoLimits.PermissionsMaxLength)} before adding more permissions.");
  }

  /// <summary>
  /// The internal LogonTokenRequestDto.Scopes deliberately carries no [MaxLength]; the count cap
  /// there is enforced by LogonTokenScopeService.PrepareScopes. This mirrors the service guard so a
  /// future DTO change that adds an attribute here does not silently duplicate the boundary.
  /// </summary>
  [Fact]
  public void LogonTokenRequestDto_Scopes_HasNoMaxLengthAttribute()
  {
    var property = typeof(InternalDtos.LogonTokenRequestDto).GetProperty(nameof(InternalDtos.LogonTokenRequestDto.Scopes));
    Assert.NotNull(property);

    var attribute = property!.GetCustomAttribute<MaxLengthAttribute>();

    Assert.Null(attribute);
  }

  /// <summary>
  /// Pins the actual DataAnnotations behavior behind the claim formerly made in
  /// LogonTokenScopeService.PrepareScopes. MaxLengthAttribute fails validation for any type
  /// whose Count property exceeds the limit (string, ICollection, or a Count property), so
  /// [MaxLength(PermissionsMaxLength)] on the V1 DTO's read-only string permission list IS
  /// enforced by ASP.NET model binding. The service guard protects the internal LogonTokenRequestDto
  /// path, whose Scopes property carries no [MaxLength].
  /// </summary>
  [Fact]
  public void MaxLengthAttribute_OnIReadOnlyList_IsEnforced()
  {
    var values = new List<string>(capacity: DtoLimits.PermissionsMaxLength + 1);
    for (var i = 0; i < DtoLimits.PermissionsMaxLength + 1; i++)
    {
      values.Add($"permission.{i}");
    }

    var attribute = new MaxLengthAttribute(DtoLimits.PermissionsMaxLength);

    Assert.False(attribute.IsValid(values));
    Assert.True(attribute.IsValid(values.Take(DtoLimits.PermissionsMaxLength).ToList()));
  }

  [Fact]
  public void ReplacePermissionAssignmentsRequestDto_Assignments_HasMaxLength()
  {
    var property = typeof(InternalDtos.ReplacePermissionAssignmentsRequestDto)
      .GetProperty(nameof(InternalDtos.ReplacePermissionAssignmentsRequestDto.Assignments));
    Assert.NotNull(property);

    var attribute = property!.GetCustomAttribute<MaxLengthAttribute>();
    Assert.NotNull(attribute);
    Assert.Equal(DtoLimits.PermissionAssignmentIdsMaxCount, attribute!.Length);
  }
}
