using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

/// <summary>
/// Serializes <see cref="OperationFailureCode" /> as its kebab-case wire name. An unrecognized string
/// reads back as <see cref="OperationFailureCode.Unknown" /> so a newer agent cannot break an older
/// server, and the integer path is left to the default converter for MessagePack.
/// </summary>
public sealed class OperationFailureCodeJsonConverter : JsonConverter<OperationFailureCode>
{
  private static readonly Dictionary<string, OperationFailureCode> _fromWire = BuildFromWire();
  private static readonly Dictionary<OperationFailureCode, string> _toWire = BuildToWire();

  public override OperationFailureCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    return reader.TokenType switch
    {
      JsonTokenType.String =>
        reader.GetString() is { } value && _fromWire.TryGetValue(value, out var code) ? code : OperationFailureCode.Unknown,
      JsonTokenType.Number => reader.TryGetInt32(out var number)
        ? Enum.IsDefined(typeof(OperationFailureCode), number)
          ? (OperationFailureCode)number
          : OperationFailureCode.Unknown
        : OperationFailureCode.Unknown,
      _ => OperationFailureCode.Unknown
    };
  }

  public override void Write(Utf8JsonWriter writer, OperationFailureCode value, JsonSerializerOptions options)
  {
    writer.WriteStringValue(_toWire.TryGetValue(value, out var wire) ? wire : ToKebab(value.ToString()));
  }

  private static Dictionary<string, OperationFailureCode> BuildFromWire()
  {
    var map = new Dictionary<string, OperationFailureCode>(StringComparer.OrdinalIgnoreCase);

    foreach (var (member, wire) in BuildToWire())
    {
      map[wire] = member;
    }

    return map;
  }

  private static Dictionary<OperationFailureCode, string> BuildToWire()
  {
    var map = new Dictionary<OperationFailureCode, string>();

    foreach (var member in Enum.GetValues<OperationFailureCode>())
    {
      var name = member.ToString();
      var attribute = typeof(OperationFailureCode)
        .GetField(name)?
        .GetCustomAttribute<EnumMemberAttribute>()?
        .Value;

      map[member] = string.IsNullOrEmpty(attribute) ? ToKebab(name) : attribute;
    }

    return map;
  }

  private static string ToKebab(string pascal)
  {
    var builder = new StringBuilder(pascal.Length + 4);

    for (var index = 0; index < pascal.Length; index++)
    {
      var character = pascal[index];

      if (char.IsUpper(character) && index > 0)
      {
        builder.Append('-');
      }

      builder.Append(char.ToLowerInvariant(character));
    }

    return builder.ToString();
  }
}
