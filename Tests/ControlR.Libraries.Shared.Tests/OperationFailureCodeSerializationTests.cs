using System.Text.Json;
using ControlR.Libraries.Api.Contracts.Dtos;
using ControlR.Libraries.Api.Contracts.Enums;
using MessagePack;

namespace ControlR.Libraries.Shared.Tests;

/// <summary>
/// The machine-readable file-system failure code (#247) must serialize as a stable kebab string on the
/// JSON surface and survive the MessagePack hub transport, including payloads from an agent that predates
/// the field (which must read back as Unknown, not throw).
/// </summary>
public class OperationFailureCodeSerializationTests
{
  [Fact]
  public void Enum_BoxedAsObject_StillSerializesAsItsKebabWireName()
  {
    var extensions = new Dictionary<string, object?> { ["failureCode"] = OperationFailureCode.NotFound };

    Assert.Contains("\"failureCode\":\"not-found\"", JsonSerializer.Serialize(extensions));
  }

  [Theory]
  [InlineData("\"unknown\"", OperationFailureCode.Unknown)]
  [InlineData("\"not-found\"", OperationFailureCode.NotFound)]
  [InlineData("\"already-exists\"", OperationFailureCode.AlreadyExists)]
  [InlineData("\"permission-denied\"", OperationFailureCode.PermissionDenied)]
  [InlineData("\"invalid-input\"", OperationFailureCode.InvalidInput)]
  [InlineData("\"device-failure\"", OperationFailureCode.DeviceFailure)]
  [InlineData("\"device-busy\"", OperationFailureCode.DeviceBusy)]
  [InlineData("\"device-offline\"", OperationFailureCode.DeviceOffline)]
  public void Enum_DeserializesFromItsKebabWireName(string json, OperationFailureCode expected)
  {
    Assert.Equal(expected, JsonSerializer.Deserialize<OperationFailureCode>(json));
  }

  [Fact]
  public void Enum_ForAnUnrecognizedWireName_Throws()
  {
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<OperationFailureCode>("\"a-code-from-the-future\""));
  }

  [Theory]
  [InlineData(OperationFailureCode.Unknown, "unknown")]
  [InlineData(OperationFailureCode.NotFound, "not-found")]
  [InlineData(OperationFailureCode.AlreadyExists, "already-exists")]
  [InlineData(OperationFailureCode.PermissionDenied, "permission-denied")]
  [InlineData(OperationFailureCode.InvalidInput, "invalid-input")]
  [InlineData(OperationFailureCode.DeviceFailure, "device-failure")]
  [InlineData(OperationFailureCode.DeviceBusy, "device-busy")]
  [InlineData(OperationFailureCode.DeviceOffline, "device-offline")]
  public void Enum_SerializesAsItsKebabWireName(OperationFailureCode code, string wireName)
  {
    Assert.Equal($"\"{wireName}\"", JsonSerializer.Serialize(code));
  }

  [Fact]
  public void HubResult_CurrentPayload_DeserializesIntoALegacyServer()
  {
    var current = HubResult.Fail("a refusal with a code", OperationFailureCode.NotFound);
    var bytes = MessagePackSerializer.Serialize(current, cancellationToken: TestContext.Current.CancellationToken);

    var legacyRead = MessagePackSerializer.Deserialize<LegacyHubResultPayload>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(legacyRead.IsSuccess);
    Assert.Equal("a refusal with a code", legacyRead.Reason);
    Assert.Null(legacyRead.ErrorCode);
  }

  [Fact]
  public void HubResult_FromAnAgentWithoutTheField_ReadsBackAsUnknown()
  {
    var legacy = new LegacyHubResultPayload(
      IsSuccess: false,
      Reason: "an old agent refusal",
      ErrorCode: Guid.NewGuid());
    var bytes = MessagePackSerializer.Serialize(legacy, cancellationToken: TestContext.Current.CancellationToken);

    var restored = MessagePackSerializer.Deserialize<HubResult>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(OperationFailureCode.Unknown, restored.FailureCode);
    Assert.Equal("an old agent refusal", restored.Reason);
  }

  [Fact]
  public void HubResult_RoundTripsOverMessagePackPreservingTheFailureCode()
  {
    var original = HubResult.Fail("no such path", OperationFailureCode.NotFound);

    var bytes = MessagePackSerializer.Serialize(original, cancellationToken: TestContext.Current.CancellationToken);
    var restored = MessagePackSerializer.Deserialize<HubResult>(
      bytes,
      cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(OperationFailureCode.NotFound, restored.FailureCode);
    Assert.Equal("no such path", restored.Reason);
  }

  /// <summary>
  /// The hub result shape as it existed before #247: no FailureCode key, but the old ErrorCode present.
  /// </summary>
  [MessagePackObject(keyAsPropertyName: true, AllowPrivate = true)]
  internal sealed record LegacyHubResultPayload(bool IsSuccess, string? Reason, Guid? ErrorCode);
}
