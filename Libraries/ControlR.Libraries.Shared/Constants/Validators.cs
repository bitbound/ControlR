using System.Text.RegularExpressions;

namespace ControlR.Libraries.Shared.Constants;

public static partial class Validators
{
  [GeneratedRegex("[^A-Za-z0-9 _-]")]
  public static partial Regex DisplayNameValidator();
  
  [GeneratedRegex("[^a-z0-9-]")]
  public static partial Regex TagNameValidator();

  [GeneratedRegex("[^A-Za-z0-9_-]")]
  public static partial Regex UsernameValidator();

}