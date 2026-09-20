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
  [Theory]
  [InlineData("\"not-found\"", OperationFailureCode.NotFound)]
  [InlineData("\"already-exists\"", OperationFailureCode.AlreadyExists)]
  [InlineData("\"a-code-from-the-future\"", OperationFailureCode.Unknown)]
  public void Enum_DeserializesFromWireNameOrFallsBackToUnknown(string json, OperationFailureCode expected)
  {
    Assert.Equal(expected, JsonSerializer.Deserialize<OperationFailureCode>(json));
  }

  [Theory]
  [InlineData(OperationFailureCode.NotFound, "not-found")]
  [InlineData(OperationFailureCode.AlreadyExists, "already-exists")]
  [InlineData(OperationFailureCode.PermissionDenied, "permission-denied")]
  [InlineData(OperationFailureCode.InvalidInput, "invalid-input")]
  [InlineData(OperationFailureCode.DeviceFailure, "device-failure")]
  public void Enum_SerializesAsItsKebabWireName(OperationFailureCode code, string wireName)
  {
    Assert.Equal($"\"{wireName}\"", JsonSerializer.Serialize(code));
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
