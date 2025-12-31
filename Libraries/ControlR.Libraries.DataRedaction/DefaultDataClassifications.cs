using Microsoft.Extensions.Compliance.Classification;

namespace ControlR.Libraries.DataRedaction;

public static class DefaultDataClassifications
{
  public static string Name => "DefaultDataClassifications";
  public static DataClassification Protected => new(Name, nameof(Protected));
  public static DataClassification Public => new(Name, nameof(Public));
}
