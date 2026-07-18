using Microsoft.AspNetCore.HttpLogging;

namespace ControlR.Web.Server.Startup;

public static class HttpLoggingRegistrationExtensions
{
  public static void AddControlrHttpLogging(
    this IHostApplicationBuilder hostBuilder,
    AppOptions appOptions)
  {
    if (!appOptions.UseHttpLogging)
    {
      return;
    }

    hostBuilder.Services.AddHttpLogging(options =>
    {
      options.RequestHeaders.Add("X-Forwarded-For");
      options.RequestHeaders.Add("X-Forwarded-Proto");
      options.RequestHeaders.Add("X-Forwarded-Host");
      options.RequestHeaders.Add("X-Original-For");
      options.RequestHeaders.Add("X-Original-Proto");
      options.RequestHeaders.Add("X-Original-Host");
      options.RequestHeaders.Add("CF-Connecting-IP");
      options.RequestHeaders.Add("CF-RAY");
      options.RequestHeaders.Add("CF-IPCountry");
      options.RequestHeaders.Add("CDN-Loop");
      options.LoggingFields = HttpLoggingFields.All ^ HttpLoggingFields.RequestQuery;
    });
  }
}