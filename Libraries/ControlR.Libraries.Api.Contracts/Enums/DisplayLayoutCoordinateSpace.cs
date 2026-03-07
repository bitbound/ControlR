using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DisplayLayoutCoordinateSpace
{
  Logical,
  Physical,
}