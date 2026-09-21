using System.Text.RegularExpressions;

namespace ControlR.ApiClient.Tests;

/// <summary>
/// Pins XML doc summaries to two sentences in the folders listed as cleaned. Adding a folder to the
/// clean set forces its summaries to be trimmed; new contract detail belongs in remarks, not summaries.
/// </summary>
public sealed partial class XmlDocSummaryLengthGuardTests
{
  private const string RepoRootMarker = "ControlR.slnx";

  // Top-level source folders whose summaries already obey the two-sentence rule. Every other folder is
  // grandfathered debt until it is cleaned and added here.
  private static readonly string[] _cleanedFolders =
  [
    "ControlR.ApiClient",
  ];
  private static readonly string[] _ignoredDirs =
  [
    "bin", "obj", "node_modules", "novnc",
  ];

  [Fact]
  public void CleanedFolders_HaveNoOverLongSummaries()
  {
    var root = FindRepositoryRoot();
    var offenders = new List<string>();

    foreach (var folder in _cleanedFolders)
    {
      var folderRoot = Path.Combine(root, folder);
      if (!Directory.Exists(folderRoot))
      {
        offenders.Add($"cleaned folder no longer exists: {folder}");
        continue;
      }

      foreach (var file in EnumerateSourceFiles(folderRoot))
      {
        foreach (var (line, text) in EnumerateSummaries(File.ReadAllLines(file)))
        {
          if (CountSentences(text) > 2)
          {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            offenders.Add($"{relative}:{line} ({CountSentences(text)} sentences)");
          }
        }
      }
    }

    Assert.True(
      offenders.Count == 0,
      "XML doc summaries in cleaned folders must be one or two sentences stating what the member is. " +
      "Move contract detail (status codes, media types, cross-endpoint comparisons) into the code or <remarks>. " +
      $"Offenders:\n  {string.Join("\n  ", offenders)}");
  }

  private static int CountSentences(string summary)
  {
    var text = StripTags(summary);
    if (string.IsNullOrWhiteSpace(text))
    {
      return 0;
    }

    var sentences = SentenceSplitter().Split(text);
    return sentences.Count(s => !string.IsNullOrWhiteSpace(s));
  }

  private static IEnumerable<string> EnumerateSourceFiles(string folderRoot)
  {
    return Directory
      .EnumerateFiles(folderRoot, "*.cs", SearchOption.AllDirectories)
      .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(_ignoredDirs.Contains));
  }

  private static IEnumerable<(int Line, string Text)> EnumerateSummaries(string[] lines)
  {
    for (var i = 0; i < lines.Length; i++)
    {
      var trimmed = lines[i].Trim();
      if (!trimmed.StartsWith("/// <summary>", StringComparison.Ordinal))
      {
        continue;
      }

      var body = new List<string>();
      var inlineClose = trimmed.IndexOf("</summary>", StringComparison.Ordinal);
      if (inlineClose >= 0)
      {
        var single = trimmed[("/// <summary>".Length)..inlineClose];
        yield return (i + 1, single.Trim());
        continue;
      }

      body.Add(trimmed[("/// <summary>".Length)..]);
      var j = i + 1;
      for (; j < lines.Length; j++)
      {
        var line = lines[j].Trim();
        if (line.Contains("</summary>", StringComparison.Ordinal))
        {
          break;
        }

        body.Add(line.StartsWith("///", StringComparison.Ordinal) ? line[3..] : line);
      }

      yield return (i + 1, string.Join(" ", body.Select(x => x.Trim())));
      i = j;
    }
  }

  private static string FindRepositoryRoot()
  {
    var current = new DirectoryInfo(AppContext.BaseDirectory);
    while (current is not null)
    {
      if (File.Exists(Path.Combine(current.FullName, RepoRootMarker)))
      {
        return current.FullName;
      }

      current = current.Parent;
    }

    throw new DirectoryNotFoundException($"Could not locate repository root containing {RepoRootMarker}.");
  }

  // Splits on a terminator followed by whitespace and a capital letter, so version numbers and
  // abbreviations inside a sentence do not inflate the count.
  [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z])")]
  private static partial Regex SentenceSplitter();

  private static string StripTags(string value)
  {
    var noTags = TagRegex().Replace(value, " ");
    return noTags.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&").Trim();
  }

  [GeneratedRegex(@"<[^>]+>")]
  private static partial Regex TagRegex();
}
