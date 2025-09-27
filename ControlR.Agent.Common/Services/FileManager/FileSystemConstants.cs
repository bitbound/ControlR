using System.Collections.Immutable;

namespace ControlR.Agent.Common.Services.FileManager;

internal static class FileSystemConstants
{
  public static ImmutableList<string> ExcludedDrivePrefixes { get; } =
  [
    "/System/Volumes",
    "/snap",
    "/boot",
    "/var/lib/docker"
  ];  
}