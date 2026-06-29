using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Client.Authz;
using ControlR.Web.Server.Api;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;
using Xunit;

namespace ControlR.Web.Server.Tests;

public class ScriptsControllerTests(ITestOutputHelper testOutput)
{
  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CreateScript_SavesScriptToDatabase()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.Services.CreateScope();
    var (controller, tenant, user) = await scope.CreateControllerWithTestData<ScriptsController>(roles: [RoleNames.TenantAdministrator]);
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    var request = new ScriptCreateRequestDto(
      Name: "Test Script",
      Description: "Test Description",
      CodeContent: "echo 'hello'",
      ShellType: ShellType.PowerShell,
      TimeoutSeconds: 300);

    // Act
    var result = await controller.CreateScript(db, request);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dto = Assert.IsType<ScriptDto>(okResult.Value);
    Assert.Equal("Test Script", dto.Name);
    Assert.Equal("echo 'hello'", dto.CodeContent);

    var savedScript = await db.Scripts.FirstOrDefaultAsync(x => x.Id == dto.Id, TestContext.Current.CancellationToken);
    Assert.NotNull(savedScript);
    Assert.Equal("Test Script", savedScript.Name);
  }

  [Fact]
  public async Task ExecuteScript_CreatesScriptExecutionRecord()
  {
    // Arrange
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.Services.CreateScope();
    var (controller, tenant, user) = await scope.CreateControllerWithTestData<ScriptsController>(roles: [RoleNames.TenantAdministrator]);
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AppDb>();

    var deviceId = Guid.NewGuid();
    var device = await services.CreateTestDevice(tenant.Id, deviceId);
    device.IsOnline = true;
    device.ConnectionId = "test-conn";
    db.Entry(device).State = EntityState.Modified;
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var script = new Data.Entities.Script
    {
      TenantId = tenant.Id,
      Name = "Run Me",
      CodeContent = "whoami",
      ShellType = ShellType.PowerShell
    };
    await db.Scripts.AddAsync(script, TestContext.Current.CancellationToken);
    await db.SaveChangesAsync(TestContext.Current.CancellationToken);

    var mockAgentClients = new Mock<IHubClients<IAgentHubClient>>();
    var mockAgentClient = new Mock<IAgentHubClient>();
    mockAgentClients.Setup(x => x.Client(It.IsAny<string>())).Returns(mockAgentClient.Object);
    var mockAgentHubContext = new Mock<IHubContext<AgentHub, IAgentHubClient>>();
    mockAgentHubContext.Setup(x => x.Clients).Returns(mockAgentClients.Object);

    var timeProvider = services.GetRequiredService<TimeProvider>();

    // Act
    var result = await controller.ExecuteScript(db, mockAgentHubContext.Object, timeProvider, script.Id, [deviceId]);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result.Result);
    var dtos = Assert.IsType<ScriptExecutionDto[]>(okResult.Value);
    Assert.Single(dtos);

    var executionDto = dtos[0];
    Assert.Equal(script.Id, executionDto.ScriptId);
    Assert.Equal(deviceId, executionDto.DeviceId);
    Assert.Equal(ScriptStatus.Running, executionDto.Status);

    mockAgentClient.Verify(x => x.ExecuteScript(executionDto.Id, script.CodeContent, script.ShellType, ScriptRunAs.System), Times.Once);
  }
}
