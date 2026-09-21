using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

/// <summary>
/// A machine-readable label for why an operation failed on the device, carried on
/// <see cref="Dtos.HubResult" /> so the server can act on the reason instead of parsing the message.
/// </summary>
/// <remarks>
/// Values are explicitly numbered with gaps so new members can be inserted without changing existing
/// MessagePack wire values. The JSON name is the kebab-case <see cref="EnumMemberAttribute" /> value.
/// </remarks>
[JsonConverter(typeof(OperationFailureCodeJsonConverter))]
public enum OperationFailureCode
{
  /// <summary>
  /// No code was supplied, such as a refusal from an agent predating this field.
  /// </summary>
  [EnumMember(Value = "unknown")]
  Unknown = 0,

  /// <summary>
  /// The target path does not exist.
  /// </summary>
  [EnumMember(Value = "not-found")]
  NotFound = 10,

  /// <summary>
  /// A resource with the requested name already exists.
  /// </summary>
  [EnumMember(Value = "already-exists")]
  AlreadyExists = 20,

  /// <summary>
  /// The device refused the operation for a permission reason.
  /// </summary>
  [EnumMember(Value = "permission-denied")]
  PermissionDenied = 30,

  /// <summary>
  /// The input was rejected on the device before it could be attempted.
  /// </summary>
  [EnumMember(Value = "invalid-input")]
  InvalidInput = 40,

  /// <summary>
  /// The operation reached the device and failed there (I/O, resource, or an unexpected device error).
  /// </summary>
  [EnumMember(Value = "device-failure")]
  DeviceFailure = 50,
}
