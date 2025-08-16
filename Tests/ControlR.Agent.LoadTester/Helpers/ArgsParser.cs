namespace ControlR.Agent.LoadTester.Helpers;
internal static class ArgsParser
{
  private static readonly Dictionary<string, string> _args = [];

  public static TValue GetArgValue<TValue>(string argName, TValue? defaultValue = default)
  {
    BuildArgs();

    var sanitizedName = argName.TrimStart('-', '-').ToLower();

    if (!_args.TryGetValue(sanitizedName, out var value))
    {
      if (defaultValue is not null)
      {
        return defaultValue;
      }
      throw new ArgumentException($"Argument '{sanitizedName}' not found.");
    }

    return (TValue)Convert.ChangeType(value, typeof(TValue));
  }

  private static void BuildArgs()
  {
    lock (_args)
    {
      if (_args.Count > 0)
      {
        return;
      }

      var args = Environment.CommandLine.Split(' ');
      for (var i = 0; i < args.Length; i++)
      {
        if (!args[i].StartsWith("--"))
        {
          continue;
        }

        var argName = args[i].Substring(2);
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
        {
          _args[argName] = args[i + 1].ToLower();
        }
        else
        {
          _args[argName] = "true"; // Switch-type argument
        }
      }
    }
  }
}
