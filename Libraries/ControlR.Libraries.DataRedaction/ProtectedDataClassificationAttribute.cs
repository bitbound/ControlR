using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.DependencyInjection;

namespace ControlR.Libraries.DataRedaction;

/// <summary>
/// Specifies that the associated data is classified as protected, indicating it requires special handling to ensure
/// confidentiality and integrity.
/// </summary>
/// <remarks>
/// When using <see cref="ServiceCollectionExtensions.AddStarRedactor(IServiceCollection)"/>,
/// members marked with this attribute will be redacted by the <see cref="StarRedactor"/> during logging operations.
/// </remarks>
public class ProtectedDataClassificationAttribute : DataClassificationAttribute
{
  public ProtectedDataClassificationAttribute()
    : base(DefaultDataClassifications.Protected)
  {
  }
}
