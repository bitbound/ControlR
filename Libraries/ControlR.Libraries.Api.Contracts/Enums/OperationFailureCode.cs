using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

/// <summary>
/// A machine-readable label for why an operation failed on the device, carried on
/// <see cref="Dtos.HubResult" /> so the server can act on the reason instead of parsing the message.
/// </summary>
/// <remarks>
/// Values are explicitly numbered with gaps so new members can be inserted without changing existing
/// MessagePack wire values. The JSON name is the kebab-case <see cref="JsonStringEnumMemberNameAttribute" /> value.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationFailureCode
{
  /// <summary>
  /// No code was supplied, such as a refusal from an agent predating this field.
  /// </summary>
  [JsonStringEnumMemberName("unknown")]
  Unknown = 0,

  /// <summary>
  /// The target path does not exist.
  /// </summary>
  [JsonStringEnumMemberName("not-found")]
  NotFound = 10,

  /// <summary>
  /// A resource with the requested name already exists.
  /// </summary>
  [JsonStringEnumMemberName("already-exists")]
  AlreadyExists = 20,

  /// <summary>
  /// The device refused the operation for a permission reason.
  /// </summary>
  [JsonStringEnumMemberName("permission-denied")]
  PermissionDenied = 30,

  /// <summary>
  /// The input was rejected on the device before it could be attempted.
  /// </summary>
  [JsonStringEnumMemberName("invalid-input")]
  InvalidInput = 40,

  /// <summary>
  /// The operation reached the device and failed there (I/O, resource, or an unexpected device error).
  /// </summary>
  [JsonStringEnumMemberName("device-failure")]
  DeviceFailure = 50,
  /// <summary>
  /// The device holds the target and will not release it, such as a file opened by another process. The
  /// operation would succeed if retried after the lock is let go.
  /// </summary>
  [JsonStringEnumMemberName("device-busy")]
  DeviceBusy = 55,
  /// <summary>
  /// The device is not connected, so nobody could carry out the operation. The server supplies this for
  /// its own hub results; an agent cannot report its own disconnection.
  /// </summary>
  [JsonStringEnumMemberName("device-offline")]
  DeviceOffline = 60,
}
