using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ControlR.ApiClient;
using ControlR.Libraries.Api.Contracts.Dtos.Devices;
using ControlR.Libraries.Api.Contracts.Dtos.ServerApi;
using ControlR.Libraries.Api.Contracts.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ControlR.Web.Server.Tests;

public class ControlrApiResponseDtoStrictnessTests(ITestOutputHelper testOutputHelper)
{
  private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

  [Fact]
  public async Task GetAllDevices_ManualPerformanceComparison_WithAndWithoutPerItemValidation()
  {
    using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    const int dtoCount = 20000;
    var payload = Enumerable.Range(0, dtoCount)
      .Select(CreateDeviceResponseDto)
      .ToArray();

    var responseJson = JsonSerializer.Serialize(payload);

    var withoutValidationApi = CreateClient(
      responseJson,
      disableResponseDtoStrictness: false,
      disableStreamingResponseDtoStrictness: true);
    var withoutValidationCount = 0;
    var withoutValidationTimer = Stopwatch.StartNew();

    await foreach (var device in withoutValidationApi.Devices.GetAllDevices(testCts.Token))
    {
      if (device is null)
      {
        continue;
      }

      withoutValidationCount++;
    }

    withoutValidationTimer.Stop();

    var withValidationApi = CreateClient(
      responseJson,
      disableResponseDtoStrictness: false,
      disableStreamingResponseDtoStrictness: false);
    var withValidationCount = 0;
    var withValidationTimer = Stopwatch.StartNew();

    await foreach (var device in withValidationApi.Devices.GetAllDevices(testCts.Token))
    {
      if (device is null)
      {
        continue;
      }

      withValidationCount++;
    }

    withValidationTimer.Stop();

    _testOutputHelper.WriteLine($"Streamed DTO count: {dtoCount}");
    _testOutputHelper.WriteLine($"Without per-item validation: {withoutValidationTimer.ElapsedMilliseconds} ms");
    _testOutputHelper.WriteLine($"With per-item validation: {withValidationTimer.ElapsedMilliseconds} ms");

    Assert.Equal(dtoCount, withoutValidationCount);
    Assert.Equal(dtoCount, withValidationCount);
  }

  [Fact]
  public async Task GetAllDevices_StreamingValidationDisabled_AllowsInvalidResponseDto()
  {
    var validDtos = new[]
    {
      CreateDeviceResponseDto(1),
      CreateDeviceResponseDto(2)
    };

    var invalidArrayNode = JsonSerializer.SerializeToNode(validDtos)?.AsArray() ?? throw new InvalidOperationException("Failed to create JSON array node.");
    invalidArrayNode[1]![nameof(DeviceResponseDto.Name)] = null;

    var api = CreateClient(
      invalidArrayNode.ToJsonString(),
      disableResponseDtoStrictness: false,
      disableStreamingResponseDtoStrictness: true);

    using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var streamedDevices = new List<DeviceResponseDto>();
    await foreach (var device in api.Devices.GetAllDevices(testCts.Token))
    {
      streamedDevices.Add(device);
    }

    Assert.Equal(2, streamedDevices.Count);
    Assert.Null(streamedDevices[1].Name);
  }

  [Fact]
  public async Task GetAllDevices_StrictStreamingValidationEnabled_Throws_ForInvalidResponseDto()
  {
    var validDtos = new[]
    {
      CreateDeviceResponseDto(1),
      CreateDeviceResponseDto(2)
    };

    var invalidArrayNode = JsonSerializer.SerializeToNode(validDtos)?.AsArray() ?? throw new InvalidOperationException("Failed to create JSON array node.");
    invalidArrayNode[1]![nameof(DeviceResponseDto.Name)] = null;

    var api = CreateClient(
      invalidArrayNode.ToJsonString(),
      disableResponseDtoStrictness: false,
      disableStreamingResponseDtoStrictness: false);

    using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var exception = await Assert.ThrowsAsync<InvalidDataException>(async () =>
    {
      await foreach (var _ in api.Devices.GetAllDevices(testCts.Token))
      {
      }
    });

    Assert.Contains(nameof(DeviceResponseDto.Name), exception.Message, StringComparison.Ordinal);
  }

  [Fact]
  public async Task GetDevice_StrictValidationDisabled_ReturnsSuccessfulApiResult_ForInvalidResponseDto()
  {
    var validDto = CreateDeviceResponseDto(2);
    var invalidJson = JsonSerializer.SerializeToNode(validDto)?.AsObject() ?? throw new InvalidOperationException("Failed to create JSON node.");
    invalidJson[nameof(DeviceResponseDto.Name)] = null;

    var api = CreateClient(
      invalidJson.ToJsonString(),
      disableResponseDtoStrictness: true,
      disableStreamingResponseDtoStrictness: true);

    using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var result = await api.Devices.GetDevice(Guid.NewGuid(), testCts.Token);

    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Value);
    Assert.Null(result.Value!.Name);
  }

  [Fact]
  public async Task GetDevice_StrictValidationEnabled_ReturnsFailedApiResult_ForInvalidResponseDto()
  {
    var validDto = CreateDeviceResponseDto(1);
    var invalidJson = JsonSerializer.SerializeToNode(validDto)?.AsObject() ?? throw new InvalidOperationException("Failed to create JSON node.");
    invalidJson[nameof(DeviceResponseDto.Name)] = null;

    var api = CreateClient(
      invalidJson.ToJsonString(),
      disableResponseDtoStrictness: false,
      disableStreamingResponseDtoStrictness: true);

    using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    var result = await api.Devices.GetDevice(Guid.NewGuid(), testCts.Token);

    Assert.False(result.IsSuccess);
    Assert.NotNull(result.Reason);
    Assert.Contains(nameof(DeviceResponseDto.Name), result.Reason, StringComparison.Ordinal);
  }

  private static ControlrApi CreateClient(
    string jsonResponse,
    bool disableResponseDtoStrictness,
    bool disableStreamingResponseDtoStrictness)
  {
    var httpClient = new HttpClient(new StaticJsonMessageHandler(jsonResponse))
    {
      BaseAddress = new Uri("https://localhost")
    };

    var options = new OptionsWrapper<ControlrApiClientOptions>(new ControlrApiClientOptions
    {
      BaseUrl = new Uri("https://localhost"),
      DisableResponseDtoStrictness = disableResponseDtoStrictness,
      DisableStreamingResponseDtoStrictness = disableStreamingResponseDtoStrictness
    });

    return new ControlrApi(httpClient, NullLogger<ControlrApi>.Instance, options);
  }

  private static DeviceResponseDto CreateDeviceResponseDto(int index)
  {
    return new DeviceResponseDto(
      Name: $"Device {index}",
      AgentVersion: "1.0.0",
      CpuUtilization: 10,
      Id: Guid.NewGuid(),
      Is64Bit: true,
      IsOnline: true,
      LastSeen: DateTimeOffset.UtcNow,
      OsArchitecture: Architecture.X64,
      Platform: SystemPlatform.Windows,
      ProcessorCount: 8,
      ConnectionId: $"connection-{index}",
      OsDescription: "Windows",
      TenantId: Guid.NewGuid(),
      TotalMemory: 16000,
      TotalStorage: 512000,
      UsedMemory: 8000,
      UsedStorage: 256000,
      CurrentUsers: [$"user{index}"],
      MacAddresses: [$"00:00:00:00:00:{index % 100:D2}"],
      PublicIpV4: "10.0.0.1",
      PublicIpV6: "::1",
      LocalIpV4: "192.168.1.1",
      LocalIpV6: "::1",
        Drives: [new Drive { Name = "C:\\", VolumeLabel = "System", TotalSize = 512000, FreeSpace = 256000 }],
      IsOutdated: false);
  }

  private sealed class StaticJsonMessageHandler(string jsonResponse) : HttpMessageHandler
  {
    private readonly string _jsonResponse = jsonResponse;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var response = new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(_jsonResponse, Encoding.UTF8, "application/json")
      };

      return Task.FromResult(response);
    }
  }
}
