using System.Net;
using System.Net.Http.Json;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Services;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi.V1.PermissionAssignments;

namespace ControlR.Web.Server.Tests;

public class PermissionAssignmentTenantIsolationTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task Create_CrossTenantPrincipal_ReturnsBadRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (clientA, tenantA, _, _) = await CreateTenantAdminEnvironment(testServer, "Tenant A");
    var (_, tenantB, _, _) = await CreateTenantAdminEnvironment(testServer, "Tenant B");

    // A tenant-B regular user to target.
    var userB = await testServer.Services.CreateTestUser(tenantB, $"target-{Guid.NewGuid():N}@t.local");

    // A tenant-A admin attempts to create an assignment whose target principal is a tenant-B user.
    var response = await clientA.PostAsJsonAsync(
      PaUrl(tenantA),
      new CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userB.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantA,
        null),
      TestContext.Current.CancellationToken);

    // A tenant admin must not be able to write an assignment targeting another tenant's principal
    // (fail-closed BadRequest, no row written).
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
  }

  [Fact]
  public async Task Create_CustomerScopeOnOtherTenantCustomer_ReturnsBadRequest()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (clientA, tenantA, _, userA) = await CreateTenantAdminEnvironment(testServer, "Tenant A");
    var (clientB, tenantB, _, _) = await CreateTenantAdminEnvironment(testServer, "Tenant B");

    // A customer owned by tenant B, referenced as the ScopeId of an assignment a tenant-A admin creates.
    var customerB = await CreateCustomer(testServer, tenantB, "Customer B");

    var response = await clientA.PostAsJsonAsync(
      PaUrl(tenantA),
      new CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userA.Id,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.CustomerTenant,
        customerB.Id,
        null),
      TestContext.Current.CancellationToken);

    // The cross-tenant customer must be rejected (BadRequest) — a filter/predicate regression here
    // would silently grant a permission scoped to another tenant's customer (cross-tenant leak).
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    _ = tenantA;
  }

  [Fact]
  public async Task Create_ServerScopeByTenantAdmin_ReturnsForbidden()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (clientA, tenantA, _, userA) = await CreateTenantAdminEnvironment(testServer, "Tenant A");

    var response = await clientA.PostAsJsonAsync(
      PaUrl(tenantA),
      new CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        userA.Id,
        PermissionNames.ServerPermissionsWrite,
        PermissionEffect.Allow,
        PermissionScopeKind.Server,
        null,
        null),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task Delete_CrossTenantAssignment_ReturnsNotFound()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (clientA, tenantA, _, userA) = await CreateTenantAdminEnvironment(testServer, "Tenant A");
    var (clientB, tenantB, _, _) = await CreateTenantAdminEnvironment(testServer, "Tenant B");

    var assignmentId = await CreateDeviceReadAssignment(clientA, tenantA, userA.Id);

    var response = await clientB.DeleteAsync(
      PaUrl(tenantB, $"/{assignmentId}"),
      TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

    var remaining = await GetAssignments(clientA, tenantA, userA.Id);
    Assert.Contains(remaining, a => a.Id == assignmentId);
  }

  [Fact]
  public async Task GetByPrincipal_CrossTenantPrincipal_ReturnsEmpty()
  {
    using var testServer = await TestWebServerBuilder.CreateTestServer(_testOutput);
    var (clientA, tenantA, _, userA) = await CreateTenantAdminEnvironment(testServer, "Tenant A");
    var (clientB, tenantB, _, _) = await CreateTenantAdminEnvironment(testServer, "Tenant B");

    await CreateDeviceReadAssignment(clientA, tenantA, userA.Id);

    var assignments = await GetAssignments(clientB, tenantB, userA.Id);

    Assert.Empty(assignments);
  }

  private static async Task<Customer> CreateCustomer(TestWebServer testServer, Guid tenantId, string name)
  {
    using var scope = testServer.Services.CreateScope();
    await using var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    var customer = new Customer
    {
      Name = name,
      TenantId = tenantId
    };
    db.Customers.Add(customer);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    return customer;
  }

  private static async Task<Guid> CreateDeviceReadAssignment(HttpClient client, Guid tenantId, Guid principalId)
  {
    var response = await client.PostAsJsonAsync(
      PaUrl(tenantId),
      new CreatePermissionAssignmentRequestDto(
        PermissionPrincipalKind.User,
        principalId,
        PermissionNames.DeviceRead,
        PermissionEffect.Allow,
        PermissionScopeKind.Tenant,
        tenantId,
        null),
      TestContext.Current.CancellationToken);
    response.EnsureSuccessStatusCode();

    var created = await response.Content.ReadFromJsonAsync<PermissionAssignmentDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(created);
    return created.Id;
  }

  private static async Task<PermissionAssignmentDto[]> GetAssignments(HttpClient client, Guid tenantId, Guid principalId)
  {
    var response = await client.GetAsync(
      $"{PaUrl(tenantId)}&principalKind=User&principalId={principalId}",
      TestContext.Current.CancellationToken);
    response.EnsureSuccessStatusCode();

    var assignments = await response.Content.ReadFromJsonAsync<PermissionAssignmentsResponseDto>(
      TestContext.Current.CancellationToken);
    Assert.NotNull(assignments);
    return [.. assignments.Items];
  }

  private static string PaUrl(Guid tenantId, string suffix = "") =>
    $"{HttpConstants.V1.PermissionAssignmentsEndpoint}{suffix}?tenantId={tenantId}";

  private async Task<HttpClient> CreatePatClient(TestWebServer testServer, PrincipalDescriptor actor)
  {
    var patManager = testServer.Services.GetRequiredService<IPersonalAccessTokenManager>();
    var patResult = await patManager.CreateToken(
      new InternalDtos.CreatePersonalAccessTokenRequestDto("Tenant Isolation Test PAT", PersonalAccessTokenPermissionMode.InheritOwner), actor.PrincipalId, actor);
    Assert.True(patResult.IsSuccess, $"PAT creation failed: {patResult.Reason}");

    var client = testServer.Factory.CreateClient();
    client.DefaultRequestHeaders.Add(
      PersonalAccessTokenAuthenticationSchemeOptions.DefaultHeaderName,
      patResult.Value.PlainTextToken);
    return client;
  }

  private async Task<(HttpClient Client, Guid TenantId, AppUser Admin, AppUser RegularUser)> CreateTenantAdminEnvironment(
    TestWebServer testServer, string tenantName)
  {
    var tenant = await testServer.Services.CreateTestTenant(tenantName);
    await testServer.Services.CreateTestUser(tenant.Id, email: $"seed-{Guid.NewGuid():N}@t.local");
    var admin = await testServer.Services.CreateTestUser(
      tenant.Id, $"admin-{Guid.NewGuid():N}@t.local", PermissionPresets.TenantAdministrator);
    var regularUser = await testServer.Services.CreateTestUser(
      tenant.Id, $"user-{Guid.NewGuid():N}@t.local");

    var client = await CreatePatClient(testServer, new PrincipalDescriptor(PrincipalType.User, admin.Id, admin.TenantId, "test"));
    return (client, tenant.Id, admin, regularUser);
  }
}
