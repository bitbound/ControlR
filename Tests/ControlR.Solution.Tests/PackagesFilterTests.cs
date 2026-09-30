using System.Text.Json;
using System.Xml;
using ControlR.Libraries.Shared.Helpers;

namespace ControlR.Solution.Tests;

public class PackagesFilterTests
{
  private readonly string _solutionDir;

  public PackagesFilterTests()
  {
    var solutionDirResult = IoHelper.GetSolutionDir();
    Assert.True(solutionDirResult.IsSuccess, $"Failed to find solution directory: {solutionDirResult.Reason}");
    _solutionDir = solutionDirResult.Value;
  }

  [Fact]
  public void PackagesFilter_ContainsExactlyTheNuGetPackageProjects()
  {
    var slnfPath = Path.Combine(_solutionDir, "Build", "ControlR.Packages.slnf");
    Assert.True(File.Exists(slnfPath), $"Packages solution filter not found at {slnfPath}.");

    var filterProjects = GetProjectsFromPackagesFilter(slnfPath);
    var markedProjects = GetProjectsMarkedAsNuGetPackages();

    Assert.NotEmpty(markedProjects);

    var missingFromFilter = markedProjects.Except(filterProjects, StringComparer.OrdinalIgnoreCase).ToArray();
    var extraInFilter = filterProjects.Except(markedProjects, StringComparer.OrdinalIgnoreCase).ToArray();

    Assert.True(
      missingFromFilter.Length == 0 && extraInFilter.Length == 0,
      $"ControlR.Packages.slnf is out of sync with the IsNuGetPackage markers.{Environment.NewLine}"
      + $"Missing from filter: {string.Join(", ", missingFromFilter)}{Environment.NewLine}"
      + $"Listed but not marked as a package: {string.Join(", ", extraInFilter)}");
  }

  private static HashSet<string> GetProjectsFromPackagesFilter(string slnfPath)
  {
    using var document = JsonDocument.Parse(File.ReadAllText(slnfPath));
    var projects = document.RootElement.GetProperty("solution").GetProperty("projects");

    return projects.EnumerateArray()
      .Select(x => x.GetString())
      .OfType<string>()
      .Where(x => !string.IsNullOrWhiteSpace(x))
      .Select(NormalizeRelativePath)
      .ToHashSet(StringComparer.OrdinalIgnoreCase);
  }

  private static bool IsMarkedAsNuGetPackage(string csprojPath)
  {
    var document = new XmlDocument();
    document.Load(csprojPath);

    var value = document.SelectSingleNode("//PropertyGroup/IsNuGetPackage")?.InnerText?.Trim();
    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
  }

  private static string NormalizeRelativePath(string relativePath)
  {
    return relativePath.Replace('\\', '/').Trim().TrimStart('/');
  }

  private HashSet<string> GetProjectsMarkedAsNuGetPackages()
  {
    return Directory.EnumerateFiles(_solutionDir, "*.csproj", SearchOption.AllDirectories)
      .Where(x => !IsInToolingOrBuildDirectory(x))
      .Where(IsMarkedAsNuGetPackage)
      .Select(x => NormalizeRelativePath(Path.GetRelativePath(_solutionDir, x)))
      .ToHashSet(StringComparer.OrdinalIgnoreCase);
  }

  private bool IsInToolingOrBuildDirectory(string path)
  {
    var relativeSegments = Path.GetRelativePath(_solutionDir, path)
      .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    return relativeSegments.Any(static segment =>
      segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
      || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
      || segment.StartsWith('.'));
  }
}
