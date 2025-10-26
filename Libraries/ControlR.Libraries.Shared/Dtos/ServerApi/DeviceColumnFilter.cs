using System.Diagnostics.CodeAnalysis;

namespace ControlR.Libraries.Shared.Dtos.ServerApi;

public class DeviceColumnFilter
{
  public string? Operator { get; set; }
  public string? PropertyName { get; set; }
  public string? Value { get; set; }

  [MemberNotNullWhen(true, nameof(PropertyName), nameof(Operator))]
  public bool Validate()
  {
    if (string.IsNullOrWhiteSpace(PropertyName) ||
        string.IsNullOrWhiteSpace(Operator))
    {
      return false;
    }

    return true;
  }
}