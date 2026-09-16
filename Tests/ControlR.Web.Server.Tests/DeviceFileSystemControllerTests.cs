using ControlR.Libraries.Api.Contracts.Dtos.HubDtos;
using ControlR.Libraries.Api.Contracts.Hubs.Clients;
using ControlR.Web.Server.Api.Internal;
using ControlR.Web.Server.Data;
using ControlR.Web.Server.Data.Entities;
using ControlR.Web.Server.Hubs;
using ControlR.Web.Server.Services.DeviceFileSystem;
using ControlR.Web.Server.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControlR.Web.Server.Tests;

/// <summary>
/// Characterization tests for <see cref="DeviceFileSystemController"/>. These pin the behavior that
/// exists today, including inconsistencies between sibling actions, so a later service extraction can
/// be verified against the current contract. The four binary siblings (download, download-archive,
/// logs/{deviceId}/contents, upload) stream to <c>Response.Body</c> or take multipart bodies and are
/// intentionally out of scope.
/// <para>
/// Test names are prefixed with the action method name, which is also the grouping, since member
/// ordering keeps them alphabetical. (The V1 tests in <c>Tests.V1</c> use the same convention.)
/// </para>
/// </summary>
public class DeviceFileSystemControllerTests(ITestOutputHelper testOutput)
{
  private const string OfflineMessage = "Device is not currently online.";
  private const string OnlineConnectionId = "test-agent-connection-id";

  private readonly ITestOutputHelper _testOutput = testOutput;

  [Fact]
  public async Task CreateDirectory_WhenCallerLacksFileSystemWrite_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(
      scope,
      "dfs-create-no-perm@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      new InternalDtos.CreateDirectoryRequestDto(harness.Device.Id, "/parent", "new-dir"),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  /// <remarks>
  /// This asserts 403, which the harness produces but production does not. The test-scope <c>AppDb</c>
  /// has no <c>HttpContext</c>, so the tenant filter is inactive and the foreign device is loaded,
  /// whereupon the resource policy denies it. Production filters the row first and answers a bare 404.
  /// </remarks>
  [Fact]
  public async Task CreateDirectory_WhenDeviceBelongsToAnotherTenant_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-create-foreign-device@test.local");
    var foreignTenant = await harness.Services.CreateTestTenant("Foreign Tenant");
    var foreignDevice = await harness.Services.CreateTestDevice(foreignTenant.Id);

    var result = await harness.Controller.CreateDirectory(
      foreignDevice.Id,
      new InternalDtos.CreateDirectoryRequestDto(foreignDevice.Id, "/parent", "new-dir"),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task CreateDirectory_WhenDeviceNotFound_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-create-notfound@test.local");

    var result = await harness.Controller.CreateDirectory(
      Guid.NewGuid(),
      new InternalDtos.CreateDirectoryRequestDto(Guid.NewGuid(), "/parent", "new-dir"),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task CreateDirectory_WhenDeviceOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-create-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      new InternalDtos.CreateDirectoryRequestDto(harness.Device.Id, "/parent", "new-dir"),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    var conflict = Assert.IsType<ConflictObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.Equal(OfflineMessage, conflict.Value);
  }

  [Fact]
  public async Task CreateDirectory_WhenHubCallReturnsFailure_StillReturnsNoContent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-create-discarded-failure@test.local");
    harness.AgentClient
      .Setup(x => x.CreateDirectory(It.IsAny<CreateDirectoryHubDto>()))
      .ReturnsAsync(HubResult.Fail("the agent refused"));

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      new InternalDtos.CreateDirectoryRequestDto(harness.Device.Id, "/parent", "new-dir"),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(result);
  }

  [Fact]
  public async Task CreateDirectory_WhenHubCallSucceeds_ReturnsNoContentAndForwardsHubDto()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-create-success@test.local");
    var connectionIds = new List<string>();
    harness.AgentClient
      .Setup(x => x.CreateDirectory(It.IsAny<CreateDirectoryHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      new InternalDtos.CreateDirectoryRequestDto(harness.Device.Id, "/parent", "new-dir"),
      harness.CreateDeviceFileSystem(
        Harness.CreateAgentHubContext(harness.AgentClient, connectionIds).Object),
      TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(result);
    Assert.Equal([OnlineConnectionId], connectionIds);
    harness.AgentClient.Verify(
      x => x.CreateDirectory(It.Is<CreateDirectoryHubDto>(
        dto => dto.ParentPath == "/parent" && dto.DirectoryName == "new-dir")),
      Times.Once());
  }

  [Fact]
  public async Task CreateDirectory_WhenHubCallThrows_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-create-throw@test.local");
    harness.AgentClient
      .Setup(x => x.CreateDirectory(It.IsAny<CreateDirectoryHubDto>()))
      .ThrowsAsync(new InvalidOperationException("hub boom"));

    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      new InternalDtos.CreateDirectoryRequestDto(harness.Device.Id, "/parent", "new-dir"),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred during directory creation.", objectResult.Value);
  }

  [Fact]
  public async Task CreateDirectory_WhenParentPathOrDirectoryNameMissing_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-create-missing@test.local");

    foreach (var request in new[]
    {
      new InternalDtos.CreateDirectoryRequestDto(harness.Device.Id, "", "new-dir"),
      new InternalDtos.CreateDirectoryRequestDto(harness.Device.Id, "/parent", " ")
    })
    {
      var result = await harness.Controller.CreateDirectory(
        harness.Device.Id,
        request,
        harness.DeviceFileSystem,
        TestContext.Current.CancellationToken);

      var badRequest = Assert.IsType<BadRequestObjectResult>(result);
      Assert.Equal("Parent path and directory name are required.", badRequest.Value);
    }

    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task CreateDirectory_WhenRouteDeviceIdDiffersFromBodyDeviceId_UsesRouteDeviceId()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-create-route-id@test.local");
    harness.AgentClient
      .Setup(x => x.CreateDirectory(It.IsAny<CreateDirectoryHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    // The device id in the body is never read. The route parameter drives the lookup, the
    // authorization check, and the hub call.
    var result = await harness.Controller.CreateDirectory(
      harness.Device.Id,
      new InternalDtos.CreateDirectoryRequestDto(Guid.NewGuid(), "/parent", "new-dir"),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    Assert.IsType<NoContentResult>(result);
  }

  [Fact]
  public async Task DeletePath_WhenCallerLacksFileSystemDelete_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(
      scope,
      "dfs-delete-no-perm@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      new InternalDtos.FileDeleteRequestDto(harness.Device.Id, "/parent/file.txt", false),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task DeletePath_WhenDeviceNotFound_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-delete-notfound@test.local");

    var result = await harness.Controller.DeletePath(
      Guid.NewGuid(),
      new InternalDtos.FileDeleteRequestDto(Guid.NewGuid(), "/parent/file.txt", false),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task DeletePath_WhenDeviceOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-delete-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      new InternalDtos.FileDeleteRequestDto(harness.Device.Id, "/parent/file.txt", false),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    var conflict = Assert.IsType<ConflictObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.Equal(OfflineMessage, conflict.Value);
  }

  [Fact]
  public async Task DeletePath_WhenFilePathMissing_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-delete-missing@test.local");

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      new InternalDtos.FileDeleteRequestDto(harness.Device.Id, "", false),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Equal("File path is required.", badRequest.Value);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task DeletePath_WhenHubCallReturnsFailure_StillReturnsOk()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-delete-discarded-failure@test.local");
    harness.AgentClient
      .Setup(x => x.DeleteFile(It.IsAny<FileDeleteHubDto>()))
      .ReturnsAsync(HubResult.Fail("the agent refused"));

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      new InternalDtos.FileDeleteRequestDto(harness.Device.Id, "/parent/file.txt", false),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var payload = ok.Value;
    Assert.NotNull(payload);
    Assert.Equal("File deletion completed", payload.GetType().GetProperty("Message")?.GetValue(payload));
  }

  [Fact]
  public async Task DeletePath_WhenHubCallSucceeds_ReturnsOkWithAnonymousMessageAndFilePath()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-delete-success@test.local");
    harness.AgentClient
      .Setup(x => x.DeleteFile(It.IsAny<FileDeleteHubDto>()))
      .ReturnsAsync(HubResult.Ok());

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      new InternalDtos.FileDeleteRequestDto(harness.Device.Id, "/parent/file.txt", false),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    var ok = Assert.IsType<OkObjectResult>(result);
    var payload = ok.Value;
    Assert.NotNull(payload);

    // The payload is a compiler-generated anonymous type rather than a DTO. Its property names and
    // order are the response body's JSON keys, so they are part of today's contract.
    var payloadType = payload.GetType();
    Assert.False(payloadType.IsPublic);
    var propertyNames = payloadType.GetProperties().Select(x => x.Name).ToArray();
    Assert.Equal(["Message", "FilePath"], propertyNames);
    Assert.Equal("File deletion completed", payloadType.GetProperty("Message")?.GetValue(payload));
    Assert.Equal("/parent/file.txt", payloadType.GetProperty("FilePath")?.GetValue(payload));
    harness.AgentClient.Verify(
      x => x.DeleteFile(It.Is<FileDeleteHubDto>(dto => dto.TargetPath == "/parent/file.txt")),
      Times.Once());
  }

  [Fact]
  public async Task DeletePath_WhenHubCallThrows_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-delete-throw@test.local");
    harness.AgentClient
      .Setup(x => x.DeleteFile(It.IsAny<FileDeleteHubDto>()))
      .ThrowsAsync(new InvalidOperationException("hub boom"));

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      new InternalDtos.FileDeleteRequestDto(harness.Device.Id, "/parent/file.txt", false),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred during file deletion.", objectResult.Value);
  }

  [Fact]
  public async Task DeletePath_WhenRequestMarksPathAsDirectory_ForwardsOnlyThePathToTheAgent()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-delete-isdirectory@test.local");
    var forwardedDtos = new List<FileDeleteHubDto>();
    harness.AgentClient
      .Setup(x => x.DeleteFile(It.IsAny<FileDeleteHubDto>()))
      .Callback<FileDeleteHubDto>(dto => forwardedDtos.Add(dto))
      .ReturnsAsync(HubResult.Ok());

    var result = await harness.Controller.DeletePath(
      harness.Device.Id,
      new InternalDtos.FileDeleteRequestDto(harness.Device.Id, "/parent/some-dir", IsDirectory: true),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

    Assert.IsType<OkObjectResult>(result);

    // FileDeleteRequestDto.IsDirectory has no counterpart on FileDeleteHubDto, so the flag is
    // dropped at the controller boundary. The agent only ever receives the path.
    var forwarded = Assert.Single(forwardedDtos);
    Assert.Equal(["TargetPath"], forwarded.GetType().GetProperties().Select(x => x.Name));
    Assert.Equal("/parent/some-dir", forwarded.TargetPath);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(
      scope,
      "dfs-contents-no-perm@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await GetDirectoryContentsAsync(harness, harness.Device.Id, "/parent");

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenDeviceNotFound_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-contents-notfound@test.local");

    var result = await GetDirectoryContentsAsync(harness, Guid.NewGuid(), "/parent");

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetDirectoryContents_WhenDeviceOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-contents-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await GetDirectoryContentsAsync(harness, harness.Device.Id, "/parent");

    var conflict = Assert.IsType<ConflictObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.Equal(OfflineMessage, conflict.Value);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenStreamMetadataMissing_ReturnsEmptyEntriesAndDirectoryExistsFalse()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-contents-nometa@test.local");
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ReturnsAsync((DirectoryContentsStreamRequestHubDto dto) =>
      {
        harness.HubStreamStore.GetOrCreate<InternalDtos.FileSystemEntryDto[]>(dto.StreamId).SetWriteCompleted();
        return HubResult.Ok();
      });

    var result = await GetDirectoryContentsAsync(harness, harness.Device.Id, "/missing-dir");

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<InternalDtos.GetDirectoryContentsResponseDto>(ok.Value);
    Assert.False(response.DirectoryExists);
    Assert.Empty(response.Items);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenStreamRequestFails_ReturnsBadRequestWithReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-contents-hubfail@test.local");
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ReturnsAsync(HubResult.Fail("agent could not read the directory"));

    var result = await GetDirectoryContentsAsync(harness, harness.Device.Id, "/parent");

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Equal("agent could not read the directory", badRequest.Value);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenStreamRequestReturnsNull_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-contents-null@test.local");
    HubResult? noResponse = null;
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ReturnsAsync(noResponse!);

    var result = await GetDirectoryContentsAsync(harness, harness.Device.Id, "/parent");

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred while retrieving directory contents.", objectResult.Value);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenStreamRequestThrowsOperationCanceled_ReturnsRequestTimeout()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-contents-canceled@test.local");
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ThrowsAsync(new OperationCanceledException());

    var result = await GetDirectoryContentsAsync(harness, harness.Device.Id, "/parent");

    // The timeout path uses StatusCode(int), which yields a bodyless StatusCodeResult. That
    // differs from the 500 paths, which pass a string body and therefore yield an ObjectResult.
    var timeoutResult = Assert.IsType<StatusCodeResult>(result);
    Assert.Equal(StatusCodes.Status408RequestTimeout, timeoutResult.StatusCode);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenStreamRequestThrows_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-contents-throw@test.local");
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ThrowsAsync(new InvalidOperationException("hub boom"));

    var result = await GetDirectoryContentsAsync(harness, harness.Device.Id, "/parent");

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred while retrieving directory contents.", objectResult.Value);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenStreamStallsAndRequestIsCanceled_ReturnsRequestTimeout()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-contents-stall@test.local");

    // The token is canceled from inside the hub call, which the action reaches only after the
    // device lookup and authorization have completed. That leaves the stream drain as the only
    // thing left to observe the cancellation, so the 408 branch is what gets exercised.
    using var drainCanceledCts = new CancellationTokenSource();
    using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(
      drainCanceledCts.Token,
      TestContext.Current.CancellationToken);
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ReturnsAsync(() =>
      {
        drainCanceledCts.Cancel();
        return HubResult.Ok();
      });

    var result = await harness.Controller.GetDirectoryContents(
      new InternalDtos.GetDirectoryContentsRequestDto(harness.Device.Id, "/parent"),
      harness.DeviceFileSystem,
      requestCts.Token);

    // The timeout path uses StatusCode(int), which yields a bodyless StatusCodeResult. That
    // differs from the 500 paths, which pass a string body and therefore yield an ObjectResult.
    var timeoutResult = Assert.IsType<StatusCodeResult>(result);
    Assert.Equal(StatusCodes.Status408RequestTimeout, timeoutResult.StatusCode);
  }

  [Fact]
  public async Task GetDirectoryContents_WhenStreamYieldsChunks_ReturnsFlattenedEntriesAndDirectoryExists()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-contents-success@test.local");
    var deviceIds = new List<Guid>();
    harness.AgentClient
      .Setup(x => x.StreamDirectoryContents(It.IsAny<DirectoryContentsStreamRequestHubDto>()))
      .ReturnsAsync((DirectoryContentsStreamRequestHubDto dto) =>
      {
        deviceIds.Add(dto.DeviceId);
        var signaler = harness.HubStreamStore.GetOrCreate<InternalDtos.FileSystemEntryDto[]>(dto.StreamId);
        signaler.Writer.TryWrite([CreateEntry("a.txt"), CreateEntry("b.txt")]);
        signaler.Writer.TryWrite([CreateEntry("c.txt")]);
        signaler.Metadata = true;
        signaler.SetWriteCompleted();
        return HubResult.Ok();
      });

    var result = await GetDirectoryContentsAsync(harness, harness.Device.Id, "/parent");

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<InternalDtos.GetDirectoryContentsResponseDto>(ok.Value);
    Assert.Equal([harness.Device.Id], deviceIds);
    Assert.True(response.DirectoryExists);
    Assert.Equal(["a.txt", "b.txt", "c.txt"], response.Items.Select(x => x.Name));
  }

  [Fact]
  public async Task GetLogFiles_WhenCallerLacksLogsRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(
      scope,
      "dfs-logs-no-perm@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await GetLogFilesAsync(harness, harness.Device.Id);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task GetLogFiles_WhenDeviceNotFound_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-logs-notfound@test.local");

    var result = await GetLogFilesAsync(harness, Guid.NewGuid());

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetLogFiles_WhenDeviceOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-logs-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await GetLogFilesAsync(harness, harness.Device.Id);

    var conflict = Assert.IsType<ConflictObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.Equal(OfflineMessage, conflict.Value);
  }

  [Fact]
  public async Task GetLogFiles_WhenHubCallFails_ReturnsProblemDetails()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-logs-hubfail@test.local");
    harness.AgentClient
      .Setup(x => x.GetLogFiles())
      .ReturnsAsync(HubResult.Fail<InternalDtos.GetLogFilesResponseDto>("agent log scan failed"));

    var result = await GetLogFilesAsync(harness, harness.Device.Id);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal(StatusCodes.Status500InternalServerError, problem.Status);
    Assert.Equal("A failure occurred on the remote device.", problem.Title);
    Assert.Equal("agent log scan failed", problem.Detail);
    Assert.Null(problem.Instance);
  }

  [Fact]
  public async Task GetLogFiles_WhenHubCallSucceeds_ReturnsOkWithValue()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-logs-success@test.local");
    var hubValue = new InternalDtos.GetLogFilesResponseDto(
      [new InternalDtos.LogFileGroupDto(
        "Agent",
        [new InternalDtos.LogFileEntryDto("agent.log", "/logs/agent.log", 123, DateTimeOffset.UnixEpoch)])]);
    harness.AgentClient
      .Setup(x => x.GetLogFiles())
      .ReturnsAsync(HubResult.Ok(hubValue));

    var result = await GetLogFilesAsync(harness, harness.Device.Id);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<InternalDtos.GetLogFilesResponseDto>(ok.Value);
    Assert.Same(hubValue, response);
    Assert.Equal("agent.log", response.LogFileGroups[0].LogFiles[0].FileName);
  }

  [Fact]
  public async Task GetLogFiles_WhenHubCallThrows_ReturnsProblemDetails()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-logs-throw@test.local");
    harness.AgentClient.Setup(x => x.GetLogFiles()).ThrowsAsync(new InvalidOperationException("hub boom"));

    var result = await GetLogFilesAsync(harness, harness.Device.Id);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
    Assert.Equal("Error retrieving log files.", problem.Title);
    Assert.Equal("An error occurred while retrieving log files.", problem.Detail);
  }

  [Fact]
  public async Task GetPathSegments_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(
      scope,
      "dfs-segments-no-perm@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await GetPathSegmentsAsync(harness, harness.Device.Id, "/parent");

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task GetPathSegments_WhenDeviceNotFound_ReturnsBadRequestInsteadOfNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-segments-notfound@test.local");

    var result = await GetPathSegmentsAsync(harness, Guid.NewGuid(), "/parent/child");

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Equal("Device not found.", badRequest.Value);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetPathSegments_WhenDeviceOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-segments-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await GetPathSegmentsAsync(harness, harness.Device.Id, "/parent");

    var conflict = Assert.IsType<ConflictObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.Equal(OfflineMessage, conflict.Value);
  }

  [Fact]
  public async Task GetPathSegments_WhenHubCallReturnsNull_Returns500WithNoResponseMessage()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-segments-null@test.local");
    InternalDtos.PathSegmentsResponseDto? noResponse = null;
    harness.AgentClient
      .Setup(x => x.GetPathSegments(It.IsAny<GetPathSegmentsHubDto>()))
      .ReturnsAsync(noResponse!);

    var result = await GetPathSegmentsAsync(harness, harness.Device.Id, "/parent");

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("No response received from device agent.", objectResult.Value);
  }

  [Fact]
  public async Task GetPathSegments_WhenHubCallSucceeds_ReturnsOkWithUnwrappedDto()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-segments-success@test.local");
    var hubValue = new InternalDtos.PathSegmentsResponseDto
    {
      Success = true,
      PathExists = true,
      PathSegments = ["parent", "child"],
      PathSeparator = "/",
      ErrorMessage = ""
    };
    harness.AgentClient
      .Setup(x => x.GetPathSegments(It.IsAny<GetPathSegmentsHubDto>()))
      .ReturnsAsync(hubValue);

    var result = await GetPathSegmentsAsync(harness, harness.Device.Id, "/parent/child");

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<InternalDtos.PathSegmentsResponseDto>(ok.Value);
    Assert.Same(hubValue, response);
    Assert.Equal(["parent", "child"], response.PathSegments);
    harness.AgentClient.Verify(
      x => x.GetPathSegments(It.Is<GetPathSegmentsHubDto>(dto => dto.TargetPath == "/parent/child")),
      Times.Once());
  }

  [Fact]
  public async Task GetPathSegments_WhenHubCallThrowsOperationCanceled_Returns500InsteadOfRequestTimeout()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-segments-canceled@test.local");
    harness.AgentClient
      .Setup(x => x.GetPathSegments(It.IsAny<GetPathSegmentsHubDto>()))
      .ThrowsAsync(new OperationCanceledException());

    var result = await GetPathSegmentsAsync(harness, harness.Device.Id, "/parent");

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred while getting path segments.", objectResult.Value);
  }

  [Fact]
  public async Task GetPathSegments_WhenHubCallThrows_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-segments-throw@test.local");
    harness.AgentClient
      .Setup(x => x.GetPathSegments(It.IsAny<GetPathSegmentsHubDto>()))
      .ThrowsAsync(new InvalidOperationException("hub boom"));

    var result = await GetPathSegmentsAsync(harness, harness.Device.Id, "/parent");

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred while getting path segments.", objectResult.Value);
  }

  [Fact]
  public async Task GetRootDrives_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(
      scope,
      "dfs-drives-no-perm@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await GetRootDrivesAsync(harness, harness.Device.Id);

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task GetRootDrives_WhenDeviceNotFound_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-drives-notfound@test.local");

    var result = await GetRootDrivesAsync(harness, Guid.NewGuid());

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetRootDrives_WhenDeviceOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-drives-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await GetRootDrivesAsync(harness, harness.Device.Id);

    var conflict = Assert.IsType<ConflictObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.Equal(OfflineMessage, conflict.Value);
  }

  [Fact]
  public async Task GetRootDrives_WhenHubCallFails_ReturnsBadRequestWithReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-drives-hubfail@test.local");
    harness.AgentClient
      .Setup(x => x.GetRootDrives(It.IsAny<InternalDtos.GetRootDrivesRequestDto>()))
      .ReturnsAsync(HubResult.Fail<InternalDtos.GetRootDrivesResponseDto>("no roots enumerated"));

    var result = await GetRootDrivesAsync(harness, harness.Device.Id);

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Equal("no roots enumerated", badRequest.Value);
  }

  [Fact]
  public async Task GetRootDrives_WhenHubCallReturnsNull_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-drives-null@test.local");
    HubResult<InternalDtos.GetRootDrivesResponseDto>? noResponse = null;
    harness.AgentClient
      .Setup(x => x.GetRootDrives(It.IsAny<InternalDtos.GetRootDrivesRequestDto>()))
      .ReturnsAsync(noResponse!);

    var result = await GetRootDrivesAsync(harness, harness.Device.Id);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred while retrieving root drives.", objectResult.Value);
  }

  [Fact]
  public async Task GetRootDrives_WhenHubCallSucceeds_ReturnsOkWithValue()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-drives-success@test.local");
    var hubValue = new InternalDtos.GetRootDrivesResponseDto([CreateEntry("C:")]);
    harness.AgentClient
      .Setup(x => x.GetRootDrives(It.IsAny<InternalDtos.GetRootDrivesRequestDto>()))
      .ReturnsAsync(HubResult.Ok(hubValue));

    var result = await GetRootDrivesAsync(harness, harness.Device.Id);

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<InternalDtos.GetRootDrivesResponseDto>(ok.Value);
    Assert.Same(hubValue, response);
    Assert.Equal("C:", response.Drives[0].Name);
  }

  [Fact]
  public async Task GetRootDrives_WhenHubCallThrows_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-drives-throw@test.local");
    harness.AgentClient
      .Setup(x => x.GetRootDrives(It.IsAny<InternalDtos.GetRootDrivesRequestDto>()))
      .ThrowsAsync(new InvalidOperationException("hub boom"));

    var result = await GetRootDrivesAsync(harness, harness.Device.Id);

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred while retrieving root drives.", objectResult.Value);
  }

  [Fact]
  public async Task GetSubdirectories_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(
      scope,
      "dfs-subdirs-no-perm@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await GetSubdirectoriesAsync(harness, harness.Device.Id, "/parent");

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task GetSubdirectories_WhenDeviceNotFound_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-subdirs-notfound@test.local");

    var result = await GetSubdirectoriesAsync(harness, Guid.NewGuid(), "/parent");

    Assert.IsType<NotFoundResult>(result);
    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  [Fact]
  public async Task GetSubdirectories_WhenDeviceOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-subdirs-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await GetSubdirectoriesAsync(harness, harness.Device.Id, "/parent");

    var conflict = Assert.IsType<ConflictObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.Equal(OfflineMessage, conflict.Value);
  }

  [Fact]
  public async Task GetSubdirectories_WhenStreamRequestFails_ReturnsBadRequestWithReason()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-subdirs-hubfail@test.local");
    harness.AgentClient
      .Setup(x => x.StreamSubdirectories(It.IsAny<SubdirectoriesStreamRequestHubDto>()))
      .ReturnsAsync(HubResult.Fail("agent could not enumerate subdirectories"));

    var result = await GetSubdirectoriesAsync(harness, harness.Device.Id, "/parent");

    var badRequest = Assert.IsType<BadRequestObjectResult>(result);
    Assert.Equal("agent could not enumerate subdirectories", badRequest.Value);
  }

  [Fact]
  public async Task GetSubdirectories_WhenStreamRequestThrowsOperationCanceled_ReturnsRequestTimeout()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-subdirs-canceled@test.local");
    harness.AgentClient
      .Setup(x => x.StreamSubdirectories(It.IsAny<SubdirectoriesStreamRequestHubDto>()))
      .ThrowsAsync(new OperationCanceledException());

    var result = await GetSubdirectoriesAsync(harness, harness.Device.Id, "/parent");

    // The timeout path uses StatusCode(int), which yields a bodyless StatusCodeResult. That
    // differs from the 500 paths, which pass a string body and therefore yield an ObjectResult.
    var timeoutResult = Assert.IsType<StatusCodeResult>(result);
    Assert.Equal(StatusCodes.Status408RequestTimeout, timeoutResult.StatusCode);
  }

  [Fact]
  public async Task GetSubdirectories_WhenStreamRequestThrows_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-subdirs-throw@test.local");
    harness.AgentClient
      .Setup(x => x.StreamSubdirectories(It.IsAny<SubdirectoriesStreamRequestHubDto>()))
      .ThrowsAsync(new InvalidOperationException("hub boom"));

    var result = await GetSubdirectoriesAsync(harness, harness.Device.Id, "/parent");

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred while retrieving subdirectories.", objectResult.Value);
  }

  [Fact]
  public async Task GetSubdirectories_WhenStreamStallsAndRequestIsCanceled_ReturnsRequestTimeout()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-subdirs-stall@test.local");

    // The token is canceled from inside the hub call, which the action reaches only after the
    // device lookup and authorization have completed. That leaves the stream drain as the only
    // thing left to observe the cancellation, so the 408 branch is what gets exercised.
    using var drainCanceledCts = new CancellationTokenSource();
    using var requestCts = CancellationTokenSource.CreateLinkedTokenSource(
      drainCanceledCts.Token,
      TestContext.Current.CancellationToken);
    harness.AgentClient
      .Setup(x => x.StreamSubdirectories(It.IsAny<SubdirectoriesStreamRequestHubDto>()))
      .ReturnsAsync(() =>
      {
        drainCanceledCts.Cancel();
        return HubResult.Ok();
      });

    var result = await harness.Controller.GetSubdirectories(
      new InternalDtos.GetSubdirectoriesRequestDto(harness.Device.Id, "/parent"),
      harness.DeviceFileSystem,
      requestCts.Token);

    // The timeout path uses StatusCode(int), which yields a bodyless StatusCodeResult. That
    // differs from the 500 paths, which pass a string body and therefore yield an ObjectResult.
    var timeoutResult = Assert.IsType<StatusCodeResult>(result);
    Assert.Equal(StatusCodes.Status408RequestTimeout, timeoutResult.StatusCode);
  }

  [Fact]
  public async Task GetSubdirectories_WhenStreamYieldsChunks_ReturnsFlattenedEntries()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-subdirs-success@test.local");
    var deviceIds = new List<Guid>();
    harness.AgentClient
      .Setup(x => x.StreamSubdirectories(It.IsAny<SubdirectoriesStreamRequestHubDto>()))
      .ReturnsAsync((SubdirectoriesStreamRequestHubDto dto) =>
      {
        deviceIds.Add(dto.DeviceId);
        var signaler = harness.HubStreamStore.GetOrCreate<InternalDtos.FileSystemEntryDto[]>(dto.StreamId);
        signaler.Writer.TryWrite([CreateEntry("dir-a", isDirectory: true)]);
        signaler.Writer.TryWrite([CreateEntry("dir-b", isDirectory: true)]);

        // Unlike the contents action, this one never reads Metadata, so the agent's
        // directory-exists signal is dropped on the floor.
        signaler.Metadata = false;
        signaler.SetWriteCompleted();
        return HubResult.Ok();
      });

    var result = await GetSubdirectoriesAsync(harness, harness.Device.Id, "/parent");

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<InternalDtos.GetSubdirectoriesResponseDto>(ok.Value);
    Assert.Equal([harness.Device.Id], deviceIds);
    Assert.Equal(["dir-a", "dir-b"], response.Subdirectories.Select(x => x.Name));
  }

  [Fact]
  public async Task ValidateFilePath_WhenAgentReportsInvalidPath_ReturnsOkWithUnwrappedDto()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-validate-invalid@test.local");
    var hubValue = new InternalDtos.ValidateFilePathResponseDto(false, "name contains invalid characters");
    harness.AgentClient
      .Setup(x => x.ValidateFilePath(It.IsAny<ValidateFilePathHubDto>()))
      .ReturnsAsync(hubValue);

    var result = await ValidateFilePathAsync(
      harness,
      new InternalDtos.ValidateFilePathRequestDto(harness.Device.Id, "/parent", "bad name"));

    var ok = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<InternalDtos.ValidateFilePathResponseDto>(ok.Value);
    Assert.Same(hubValue, response);
    Assert.False(response.IsValid);
    Assert.Equal("name contains invalid characters", response.ErrorMessage);
    harness.AgentClient.Verify(
      x => x.ValidateFilePath(It.Is<ValidateFilePathHubDto>(
        dto => dto.DirectoryPath == "/parent" && dto.FileName == "bad name")),
      Times.Once());
  }

  [Fact]
  public async Task ValidateFilePath_WhenCallerLacksFileSystemRead_Forbids()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(
      scope,
      "dfs-validate-no-perm@test.local",
      PermissionPresets.TenantAdministrator);

    var result = await ValidateFilePathAsync(
      harness,
      new InternalDtos.ValidateFilePathRequestDto(harness.Device.Id, "/parent", "file.txt"));

    Assert.IsType<ForbidResult>(result);
  }

  [Fact]
  public async Task ValidateFilePath_WhenDeviceNotFound_ReturnsNotFound()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-validate-notfound@test.local");

    var result = await ValidateFilePathAsync(
      harness,
      new InternalDtos.ValidateFilePathRequestDto(Guid.NewGuid(), "/parent", "file.txt"));

    Assert.IsType<NotFoundResult>(result);
  }

  [Fact]
  public async Task ValidateFilePath_WhenDeviceOffline_ReturnsConflict()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-validate-offline@test.local");
    await harness.SetDeviceOnline(isOnline: false);

    var result = await ValidateFilePathAsync(
      harness,
      new InternalDtos.ValidateFilePathRequestDto(harness.Device.Id, "/parent", "file.txt"));

    var conflict = Assert.IsType<ConflictObjectResult>(result);
    Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    Assert.Equal(OfflineMessage, conflict.Value);
  }

  [Fact]
  public async Task ValidateFilePath_WhenDirectoryPathOrFileNameMissing_ReturnsBadRequest()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-validate-missing@test.local");

    foreach (var request in new[]
    {
      new InternalDtos.ValidateFilePathRequestDto(harness.Device.Id, "", "file.txt"),
      new InternalDtos.ValidateFilePathRequestDto(harness.Device.Id, "/parent", "")
    })
    {
      var result = await ValidateFilePathAsync(harness, request);

      var badRequest = Assert.IsType<BadRequestObjectResult>(result);
      Assert.Equal("Directory path and file name are required.", badRequest.Value);
    }

    harness.AgentHub.VerifyGet(x => x.Clients, Times.Never());
  }

  /// <remarks>
  /// The service reports a reasonless rejection when the agent never answered, which this endpoint's
  /// switch folds into its 500 arm, so the shipped 500 is preserved. The 500 is the contract being
  /// pinned. The versioned surface answers the same condition with a 502 instead.
  /// </remarks>
  [Fact]
  public async Task ValidateFilePath_WhenHubCallReturnsNull_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-validate-null@test.local");
    InternalDtos.ValidateFilePathResponseDto? noResponse = null;
    harness.AgentClient
      .Setup(x => x.ValidateFilePath(It.IsAny<ValidateFilePathHubDto>()))
      .ReturnsAsync(noResponse!);

    var result = await ValidateFilePathAsync(
      harness,
      new InternalDtos.ValidateFilePathRequestDto(harness.Device.Id, "/parent", "file.txt"));

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred while validating the file path.", objectResult.Value);
  }

  [Fact]
  public async Task ValidateFilePath_WhenHubCallThrows_Returns500WithStringBody()
  {
    await using var testApp = await TestAppBuilder.CreateTestApp(_testOutput);
    using var scope = testApp.CreateScope();
    var harness = await Harness.CreateAsync(scope, "dfs-validate-throw@test.local");
    harness.AgentClient
      .Setup(x => x.ValidateFilePath(It.IsAny<ValidateFilePathHubDto>()))
      .ThrowsAsync(new InvalidOperationException("hub boom"));

    var result = await ValidateFilePathAsync(
      harness,
      new InternalDtos.ValidateFilePathRequestDto(harness.Device.Id, "/parent", "file.txt"));

    var objectResult = Assert.IsType<ObjectResult>(result);
    Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    Assert.Equal("An error occurred while validating the file path.", objectResult.Value);
  }

  private static InternalDtos.FileSystemEntryDto CreateEntry(string name, bool isDirectory = false) =>
    new(
      Name: name,
      FullPath: $"/parent/{name}",
      IsDirectory: isDirectory,
      Size: 42,
      LastModified: DateTimeOffset.UnixEpoch,
      IsHidden: false,
      CanRead: true,
      CanWrite: true,
      HasSubfolders: isDirectory);

  // Action invocations. The dependencies still arrive as [FromServices] parameters, resolved from the
  // request scope as the framework would resolve them, except that the service is built by the harness
  // because it needs the mock hub context rather than the container's real one. See
  // Harness.CreateDeviceFileSystem.
  private static async Task<IActionResult> GetDirectoryContentsAsync(
    Harness harness,
    Guid deviceId,
    string directoryPath) =>
    await harness.Controller.GetDirectoryContents(
      new InternalDtos.GetDirectoryContentsRequestDto(deviceId, directoryPath),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

  private static async Task<IActionResult> GetLogFilesAsync(Harness harness, Guid deviceId) =>
    await harness.Controller.GetLogFiles(
      deviceId,
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

  private static async Task<IActionResult> GetPathSegmentsAsync(
    Harness harness,
    Guid deviceId,
    string targetPath) =>
    await harness.Controller.GetPathSegments(
      new InternalDtos.GetPathSegmentsRequestDto(deviceId, targetPath),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

  private static async Task<IActionResult> GetRootDrivesAsync(Harness harness, Guid deviceId) =>
    await harness.Controller.GetRootDrives(
      new InternalDtos.GetRootDrivesRequestDto(deviceId),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

  private static async Task<IActionResult> GetSubdirectoriesAsync(
    Harness harness,
    Guid deviceId,
    string directoryPath) =>
    await harness.Controller.GetSubdirectories(
      new InternalDtos.GetSubdirectoriesRequestDto(deviceId, directoryPath),
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

  private static async Task<IActionResult> ValidateFilePathAsync(
    Harness harness,
    InternalDtos.ValidateFilePathRequestDto request) =>
    await harness.Controller.ValidateFilePath(
      request.DeviceId,
      request,
      harness.DeviceFileSystem,
      TestContext.Current.CancellationToken);

  /// <summary>
  /// Resolved services plus a <see cref="DeviceFileSystemController"/> wired to an authenticated
  /// caller and one online device in that caller's tenant.
  /// </summary>
  private sealed class Harness(
    DeviceFileSystemController controller,
    Device device,
    IServiceProvider services,
    AppDb db,
    Mock<IAgentHubClient> agentClient,
    Mock<IHubContext<AgentHub, IAgentHubClient>> agentHub,
    IAuthorizationService authz,
    IHubStreamStore hubStreamStore)
  {
    public Mock<IAgentHubClient> AgentClient { get; } = agentClient;

    public Mock<IHubContext<AgentHub, IAgentHubClient>> AgentHub { get; } = agentHub;

    public IAuthorizationService Authz { get; } = authz;

    public DeviceFileSystemController Controller { get; } = controller;

    public AppDb Db { get; } = db;

    public Device Device { get; } = device;

    /// <summary>
    /// The extracted service over the same dependencies the actions used to take as [FromServices]
    /// arguments, built here because the hub context each test arms is this harness's mock rather
    /// than something the container hands out.
    /// </summary>
    public IDeviceFileSystemService DeviceFileSystem => CreateDeviceFileSystem(AgentHub.Object);

    public IHubStreamStore HubStreamStore { get; } = hubStreamStore;

    public IServiceProvider Services { get; } = services;

    public static Mock<IHubContext<AgentHub, IAgentHubClient>> CreateAgentHubContext(
      Mock<IAgentHubClient> agentClient,
      List<string>? connectionIdLog = null)
    {
      var observedConnectionIds = connectionIdLog ?? [];
      var hubClients = new Mock<IHubClients<IAgentHubClient>>();
      hubClients
        .Setup(x => x.Client(It.IsAny<string>()))
        .Returns((string connectionId) =>
        {
          observedConnectionIds.Add(connectionId);
          return agentClient.Object;
        });

      var agentHub = new Mock<IHubContext<AgentHub, IAgentHubClient>>();
      agentHub.Setup(x => x.Clients).Returns(hubClients.Object);
      return agentHub;
    }

    public static async Task<Harness> CreateAsync(
      IServiceScope scope,
      string userEmail,
      params string[] presets)
    {
      var services = scope.ServiceProvider;
      string[] effectivePresets = presets.Length > 0 ? presets : [PermissionPresets.DeviceSuperUser];
      var (controller, tenant, _) = await scope.CreateControllerWithTestData<DeviceFileSystemController>(
        userEmail: userEmail,
        presets: effectivePresets);
      var device = await services.CreateTestDevice(tenant.Id);
      var agentClient = new Mock<IAgentHubClient>();

      var harness = new Harness(
        controller,
        device,
        services,
        services.GetRequiredService<AppDb>(),
        agentClient,
        CreateAgentHubContext(agentClient),
        services.GetRequiredService<IAuthorizationService>(),
        services.GetRequiredService<IHubStreamStore>());

      await harness.SetDeviceOnline(isOnline: true);
      return harness;
    }

    public IDeviceFileSystemService CreateDeviceFileSystem(
      IHubContext<AgentHub, IAgentHubClient> agentHub) =>
      new DeviceFileSystemService(
        Db,
        agentHub,
        HubStreamStore,
        Authz,
        Services.GetRequiredService<ILogger<DeviceFileSystemService>>());

    public async Task SetDeviceOnline(bool isOnline)
    {
      await using var db = Services.GetRequiredService<AppDb>();
      var device = await db.Devices.FirstAsync(
        x => x.Id == Device.Id,
        TestContext.Current.CancellationToken);

      device.IsOnline = isOnline;
      device.ConnectionId = isOnline ? OnlineConnectionId : string.Empty;
      await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
  }
}
