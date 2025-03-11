using ControlR.Web.WebSocketRelay.Dtos;
using System.Text.Json.Serialization;

namespace ControlR.Web.WebSocketRelay.Serialization;

[JsonSerializable(typeof(StatusOkDto))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{

}
