using Microsoft.Extensions.Compliance.Redaction;

namespace ControlR.Libraries.DataRedaction;

public sealed class StarRedactor : Redactor
{
  private const string Stars = "****";

  public override int GetRedactedLength(ReadOnlySpan<char> input) => Stars.Length;

  public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
  {
    Stars.CopyTo(destination);

    return Stars.Length;
  }
}