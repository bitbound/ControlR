using System.Security.Claims;
using ControlR.Web.Server.Authn;
using ControlR.Web.Server.Authz.Permissions;
using ControlR.Web.Server.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ControlR.Web.Server.Tests;

public class PermissionRequirementHandlerTests
{
  [Fact]
  public async Task HandleRequirementAsync_EvaluatorThrows_FailsClosed()
  {
    var tenantId = Guid.NewGuid();
    var principalId = Guid.NewGuid();
    var evaluator = new Mock<IPermissionEvaluator>();
    evaluator
      .Setup(x => x.EvaluateMany(
        It.IsAny<PrincipalDescriptor>(),
        It.IsAny<IReadOnlyCollection<string>>(),
        It.IsAny<ResourceDescriptor>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException("boom"));
    var handler = new PermissionRequirementHandler(
      evaluator.Object,
      Mock.Of<IResourceDescriptorFactory>(),
      Mock.Of<IHttpContextAccessor>(),
      NullLogger<PermissionRequirementHandler>.Instance);
    var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new Claim(PrincipalClaimTypes.PrincipalId, principalId.ToString()),
      new Claim(UserClaimTypes.TenantId, tenantId.ToString())
    ], "TestAuth"));
    var requirement = new PermissionRequirement(
      PermissionNames.DeviceRead,
      new ResourceDescriptor(PermissionScopeKind.Tenant, TenantId: tenantId));
    var context = new AuthorizationHandlerContext([requirement], principal, resource: requirement);

    await handler.HandleAsync(context);

    Assert.True(context.HasFailed);
  }

  [Theory]
  [InlineData(PermissionNames.DeviceGroupAssignDevices, PermissionScopeKind.DeviceGroup)]
  [InlineData(PermissionNames.UserGroupAssignUsers, PermissionScopeKind.UserGroup)]
  [InlineData(PermissionNames.TenantCustomersRead, PermissionScopeKind.CustomerTenant)]
  public async Task HandleRequirementAsync_GroupScopedGrant_EvaluatesTargetGroup(
    string permissionName,
    PermissionScopeKind scopeKind)
  {
    var tenantId = Guid.NewGuid();
    var principalId = Guid.NewGuid();
    var groupResource = new ResourceDescriptor(scopeKind, Guid.NewGuid(), tenantId);
    var evaluator = new Mock<IPermissionEvaluator>();
    evaluator
      .Setup(x => x.EvaluateMany(
        It.IsAny<PrincipalDescriptor>(),
        It.Is<IReadOnlyCollection<string>>(names => names.SequenceEqual(new[] { permissionName })),
        groupResource,
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<string, PermissionEvaluationResult>
      {
        [permissionName] = PermissionEvaluationResult.Allow("test", scopeKind.ToString())
      });
    var handler = new PermissionRequirementHandler(
      evaluator.Object,
      Mock.Of<IResourceDescriptorFactory>(),
      Mock.Of<IHttpContextAccessor>(),
      NullLogger<PermissionRequirementHandler>.Instance);
    var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new Claim(PrincipalClaimTypes.PrincipalId, principalId.ToString()),
      new Claim(UserClaimTypes.TenantId, tenantId.ToString())
    ], "TestAuth"));
    var requirement = new PermissionRequirement(permissionName, new ResourceDescriptor(scopeKind));
    var context = new AuthorizationHandlerContext([requirement], principal, groupResource);

    await handler.HandleAsync(context);

    Assert.True(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleRequirementAsync_MissingPrincipalClaims_DeniesWithoutEvaluating()
  {
    var evaluator = new Mock<IPermissionEvaluator>();
    var resourceFactory = new Mock<IResourceDescriptorFactory>();
    var httpContextAccessor = new Mock<IHttpContextAccessor>();
    httpContextAccessor.SetupGet(x => x.HttpContext).Returns(new DefaultHttpContext());

    var handler = new PermissionRequirementHandler(
      evaluator.Object,
      resourceFactory.Object,
      httpContextAccessor.Object,
      NullLogger<PermissionRequirementHandler>.Instance);

    var principal = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"));
    var requirement = new PermissionRequirement(
      PermissionNames.DeviceRead,
      new ResourceDescriptor(PermissionScopeKind.Tenant, TenantId: Guid.NewGuid()));
    var context = new AuthorizationHandlerContext(
      [requirement],
      principal,
      resource: requirement);

    await handler.HandleAsync(context);

    Assert.True(context.HasFailed);
    // The requirement must not be satisfied.
    Assert.NotEmpty(context.PendingRequirements);
    // The handler must fail closed before ever consulting the evaluator.
    evaluator.Verify(
      x => x.EvaluateMany(It.IsAny<PrincipalDescriptor>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<ResourceDescriptor>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task HandleRequirementAsync_PartialCanonicalClaims_DeniesWithoutEvaluating()
  {
    // Has a principal type but a malformed/non-Guid principal id -> descriptor is null.
    var evaluator = new Mock<IPermissionEvaluator>();
    var resourceFactory = new Mock<IResourceDescriptorFactory>();
    var httpContextAccessor = new Mock<IHttpContextAccessor>();
    httpContextAccessor.SetupGet(x => x.HttpContext).Returns(new DefaultHttpContext());

    var handler = new PermissionRequirementHandler(
      evaluator.Object,
      resourceFactory.Object,
      httpContextAccessor.Object,
      NullLogger<PermissionRequirementHandler>.Instance);

    var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new Claim(PrincipalClaimTypes.PrincipalId, "not-a-guid")
    ], "TestAuth"));
    var requirement = new PermissionRequirement(
      PermissionNames.DeviceRead,
      new ResourceDescriptor(PermissionScopeKind.Tenant, TenantId: Guid.NewGuid()));
    var context = new AuthorizationHandlerContext(
      [requirement],
      principal,
      resource: requirement);

    await handler.HandleAsync(context);

    Assert.True(context.HasFailed);
    evaluator.Verify(
      x => x.EvaluateMany(It.IsAny<PrincipalDescriptor>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<ResourceDescriptor>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task HandleRequirementAsync_UnionRequirement_AllPermissionsDenied_Fails()
  {
    var tenantId = Guid.NewGuid();
    var principalId = Guid.NewGuid();
    var tenantResource = new ResourceDescriptor(PermissionScopeKind.Tenant, tenantId, tenantId);
    var evaluator = new Mock<IPermissionEvaluator>();
    evaluator
      .Setup(x => x.EvaluateMany(
        It.IsAny<PrincipalDescriptor>(),
        It.Is<IReadOnlyCollection<string>>(names =>
          names.SequenceEqual(new[] { PermissionNames.InstallerKeySelfWrite, PermissionNames.InstallerKeyOthersWrite })),
        It.IsAny<ResourceDescriptor>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<string, PermissionEvaluationResult>
      {
        [PermissionNames.InstallerKeySelfWrite] = PermissionEvaluationResult.Deny("no grant"),
        [PermissionNames.InstallerKeyOthersWrite] = PermissionEvaluationResult.Deny("no grant")
      });
    var handler = new PermissionRequirementHandler(
      evaluator.Object,
      Mock.Of<IResourceDescriptorFactory>(),
      Mock.Of<IHttpContextAccessor>(),
      NullLogger<PermissionRequirementHandler>.Instance);
    var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new Claim(PrincipalClaimTypes.PrincipalId, principalId.ToString()),
      new Claim(UserClaimTypes.TenantId, tenantId.ToString())
    ], "TestAuth"));
    var requirement = new PermissionRequirement(
      [PermissionNames.InstallerKeySelfWrite, PermissionNames.InstallerKeyOthersWrite],
      tenantResource);
    var context = new AuthorizationHandlerContext([requirement], principal, resource: requirement);

    await handler.HandleAsync(context);

    Assert.True(context.HasFailed);
    // Fail() marks the context failed without draining pending requirements.
    Assert.NotEmpty(context.PendingRequirements);
  }

  [Fact]
  public async Task HandleRequirementAsync_UnionRequirement_EitherPermissionAllowed_Succeeds()
  {
    var tenantId = Guid.NewGuid();
    var principalId = Guid.NewGuid();
    var tenantResource = new ResourceDescriptor(PermissionScopeKind.Tenant, tenantId, tenantId);
    var evaluator = new Mock<IPermissionEvaluator>();
    evaluator
      .Setup(x => x.EvaluateMany(
        It.IsAny<PrincipalDescriptor>(),
        It.Is<IReadOnlyCollection<string>>(names =>
          names.SequenceEqual(new[] { PermissionNames.InstallerKeySelfRead, PermissionNames.InstallerKeyOthersRead })),
        It.IsAny<ResourceDescriptor>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(new Dictionary<string, PermissionEvaluationResult>
      {
        [PermissionNames.InstallerKeySelfRead] = PermissionEvaluationResult.Deny("no grant"),
        [PermissionNames.InstallerKeyOthersRead] = PermissionEvaluationResult.Allow("test", "Tenant")
      });
    var handler = new PermissionRequirementHandler(
      evaluator.Object,
      Mock.Of<IResourceDescriptorFactory>(),
      Mock.Of<IHttpContextAccessor>(),
      NullLogger<PermissionRequirementHandler>.Instance);
    var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new Claim(PrincipalClaimTypes.PrincipalId, principalId.ToString()),
      new Claim(UserClaimTypes.TenantId, tenantId.ToString())
    ], "TestAuth"));
    var requirement = new PermissionRequirement(
      [PermissionNames.InstallerKeySelfRead, PermissionNames.InstallerKeyOthersRead],
      tenantResource);
    var context = new AuthorizationHandlerContext([requirement], principal, resource: requirement);

    await handler.HandleAsync(context);

    Assert.True(context.HasSucceeded);
  }

  [Fact]
  public async Task HandleRequirementAsync_UnresolvableResource_FailsClosed()
  {
    var tenantId = Guid.NewGuid();
    var principalId = Guid.NewGuid();
    var evaluator = new Mock<IPermissionEvaluator>();
    var resourceFactory = new Mock<IResourceDescriptorFactory>();
    resourceFactory
      .Setup(x => x.CreateScope(
        PermissionScopeKind.DeviceGroup,
        It.IsAny<Guid?>(),
        tenantId,
        It.IsAny<CancellationToken>()))
      .ReturnsAsync((ResourceDescriptor?)null);
    var handler = new PermissionRequirementHandler(
      evaluator.Object,
      resourceFactory.Object,
      Mock.Of<IHttpContextAccessor>(),
      NullLogger<PermissionRequirementHandler>.Instance);
    var principal = new ClaimsPrincipal(new ClaimsIdentity(
    [
      new Claim(PrincipalClaimTypes.PrincipalType, PrincipalClaimValues.User),
      new Claim(PrincipalClaimTypes.PrincipalId, principalId.ToString()),
      new Claim(UserClaimTypes.TenantId, tenantId.ToString())
    ], "TestAuth"));
    var requirement = new PermissionRequirement(
      PermissionNames.DeviceGroupAssignDevices,
      new ResourceDescriptor(PermissionScopeKind.DeviceGroup));
    var context = new AuthorizationHandlerContext([requirement], principal, resource: requirement);

    await handler.HandleAsync(context);

    Assert.True(context.HasFailed);
    evaluator.Verify(
      x => x.EvaluateMany(It.IsAny<PrincipalDescriptor>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<ResourceDescriptor>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }
}
