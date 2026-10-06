using System.Text.Json;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// The permission-assignment enums travel on the stable V1 contract as their member names, never as
/// opaque integers. A <see cref="PermissionScopeKind"/> member name is also the string persisted in
/// PermissionAssignments.ScopeKind, so renaming a member silently invalidates stored assignments.
/// These tests pin both the wire form and the member set.
/// </summary>
public class PermissionAssignmentEnumWireFormatTests
{
  [Fact]
  public void Effect_MemberNamesAndValues_AreStable()
  {
    Assert.Equal(new[] { "Allow", "Deny" }, Enum.GetNames<PermissionEffect>());
  }

  [Fact]
  public void Effect_SerializesAsItsMemberName()
  {
    Assert.Equal("\"Allow\"", JsonSerializer.Serialize(PermissionEffect.Allow, JsonSerializerOptions.Web));
    Assert.Equal("\"Deny\"", JsonSerializer.Serialize(PermissionEffect.Deny, JsonSerializerOptions.Web));
  }

  [Fact]
  public void PermissionAssignmentDto_SerializesEnumsAsMemberNames()
  {
    var dto = new PermissionAssignmentDto(
      Guid.NewGuid(),
      PermissionPrincipalKind.User,
      Guid.NewGuid(),
      "device.read",
      PermissionEffect.Allow,
      PermissionScopeKind.Tenant,
      Guid.NewGuid(),
      Notes: null,
      IsEnabled: true,
      DateTimeOffset.UnixEpoch);

    var json = JsonSerializer.Serialize(dto, JsonSerializerOptions.Web);

    Assert.Contains("\"principalKind\":\"User\"", json);
    Assert.Contains("\"effect\":\"Allow\"", json);
    Assert.Contains("\"scopeKind\":\"Tenant\"", json);
  }

  [Fact]
  public void PermissionCatalogEntryDto_SerializesAllowedScopeKindsAsMemberNames()
  {
    var dto = new PermissionCatalogEntryDto(
      "device.read",
      "Read devices",
      "Devices",
      "Read device details.",
      [PermissionScopeKind.Tenant, PermissionScopeKind.Server],
      SelfRemovable: false);

    var json = JsonSerializer.Serialize(dto, JsonSerializerOptions.Web);

    Assert.Contains("\"allowedScopeKinds\":[\"Tenant\",\"Server\"]", json);
  }

  [Fact]
  public void PrincipalKind_MemberNamesAndValues_AreStable()
  {
    Assert.Equal(
      new[] { "User", "UserGroup", "ServiceAccount", "PersonalAccessToken", "LogonToken" },
      Enum.GetNames<PermissionPrincipalKind>());
  }

  [Fact]
  public void PrincipalKind_SerializesAsItsMemberName()
  {
    Assert.Equal(
      "\"PersonalAccessToken\"",
      JsonSerializer.Serialize(PermissionPrincipalKind.PersonalAccessToken, JsonSerializerOptions.Web));
    Assert.Equal("\"User\"", JsonSerializer.Serialize(PermissionPrincipalKind.User, JsonSerializerOptions.Web));
  }

  [Fact]
  public void ScopeKind_ForALegacyIntegerPayload_StillDeserializes()
  {
    Assert.Equal(PermissionScopeKind.Tenant, JsonSerializer.Deserialize<PermissionScopeKind>("2"));
  }

  [Fact]
  public void ScopeKind_ForAnUnrecognizedMemberName_Throws()
  {
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PermissionScopeKind>("\"DeviceFleet\""));
  }

  [Fact]
  public void ScopeKind_MemberNamesAndValues_AreStable()
  {
    Assert.Equal(
      new[] { "Unknown", "Server", "Tenant", "CustomerTenant", "DeviceGroup", "Device", "UserGroup" },
      Enum.GetNames<PermissionScopeKind>());
  }

  [Fact]
  public void ScopeKind_SerializesAsItsMemberName()
  {
    Assert.Equal("\"Tenant\"", JsonSerializer.Serialize(PermissionScopeKind.Tenant, JsonSerializerOptions.Web));
    Assert.Equal("\"Unknown\"", JsonSerializer.Serialize(PermissionScopeKind.Unknown, JsonSerializerOptions.Web));
    Assert.Equal(
      "\"CustomerTenant\"",
      JsonSerializer.Serialize(PermissionScopeKind.CustomerTenant, JsonSerializerOptions.Web));
  }
}
