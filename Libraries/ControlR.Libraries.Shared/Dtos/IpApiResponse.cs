
using System.Text.Json.Serialization;

namespace ControlR.Libraries.Shared.Dtos;

public enum IpApiResponseStatus
{
    Success,
    Fail
}

public class IpApiResponse
{
    public string? As { get; set; }
    public string? Asname { get; set; }
    public string? City { get; set; }
    public string? Continent { get; set; }
    public string? ContinentCode { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public string? Currency { get; set; }
    public string? District { get; set; }
    public bool Hosting { get; set; }
    public string? Isp { get; set; }
    public float Lat { get; set; }
    public float Lon { get; set; }
    public string? Message { get; set; }
    public bool Mobile { get; set; }
    public int Offset { get; set; }
    public string? Org { get; set; }
    public bool Proxy { get; set; }
    public string? Query { get; set; }
    public string? Region { get; set; }
    public string? RegionName { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IpApiResponseStatus Status { get; set; }

    public string? Timezone { get; set; }
    public string? Zip { get; set; }
}
