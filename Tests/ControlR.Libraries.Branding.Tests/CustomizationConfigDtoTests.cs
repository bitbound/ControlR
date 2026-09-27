using System.Text.Json;
using ControlR.Libraries.Branding.Dtos;

namespace ControlR.Libraries.Branding.Tests;

public class CustomizationConfigDtoTests
{
  private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

  [Fact]
  public void CustomCss_IsNullByDefault()
  {
    Assert.Null(new CustomizationConfigDto(BrandName: "Acme").CustomCss);
  }

  [Fact]
  public void CustomCss_RoundTripsAsCamelCase()
  {
    var dto = new CustomizationConfigDto(BrandName: "Acme", CustomCss: "body { display: flex; }");

    var json = JsonSerializer.Serialize(dto, _serializerOptions);
    var deserialized = JsonSerializer.Deserialize<CustomizationConfigDto>(json, _serializerOptions);

    Assert.Contains("\"customCss\":\"body { display: flex; }\"", json);
    Assert.Equal("body { display: flex; }", deserialized!.CustomCss);
  }

  [Fact]
  public void Payload_FromBeforeCustomCss_StillDeserializes()
  {
    const string LegacyJson = """{"brandName":"Acme","publisher":"Acme Inc."}""";

    var deserialized = JsonSerializer.Deserialize<CustomizationConfigDto>(LegacyJson, _serializerOptions);

    Assert.Null(deserialized!.CustomCss);
    Assert.Equal("Acme", deserialized.BrandName);
  }
}
