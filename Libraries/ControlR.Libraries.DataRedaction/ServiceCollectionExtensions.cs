using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ControlR.Libraries.DataRedaction;

public static class ServiceCollectionExtensions
{
  /// <summary>
  ///   Adds data redaction services and the <see cref="StarRedactor"/> to the service collection for data redaction.
  ///   Members marked with the <see cref="ProtectedDataClassificationAttribute"/> will be redacted during logging operations.
  /// </summary>
  /// <remarks>
  ///   Redaction services are provided by the Microsoft.Extensions.Compliance.Redaction package.
  ///   See <seealso href="https://learn.microsoft.com/en-us/dotnet/core/extensions/data-redaction"/> for more information.
  /// </remarks>
  public static IServiceCollection AddStarRedactor(this IServiceCollection services)
  {
    services.AddLogging(builder =>
    {
      builder.EnableRedaction();
    });

    services.AddRedaction(builder =>
    {
      builder.SetRedactor<StarRedactor>(DefaultDataClassifications.Protected);
    });
    return services;
  }
}
