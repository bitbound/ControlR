using System.Text.Json.Serialization;

namespace ControlR.Libraries.Api.Contracts.FilterSort;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FilterMatchMode
{
  Any = 0,
  All = 1
}
