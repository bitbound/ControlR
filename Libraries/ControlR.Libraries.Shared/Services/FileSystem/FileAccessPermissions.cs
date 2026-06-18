using System.Runtime.Versioning;
using System.Security.Principal;
using System.Security.AccessControl;

namespace ControlR.Libraries.Shared.Services.FileSystem;

public interface IFileAccessPermissions
{
  [SupportedOSPlatform("windows")]
  void Set(
    string filePath,
    bool includeCurrentUser,
    bool isProtected,
    bool preserveInheritance,
    WellKnownSidType? owner,
    params WellKnownSidType[] allowedSids);

  [SupportedOSPlatform("linux")]
  [SupportedOSPlatform("macos")]
  void Set(string filePath, UnixFileMode mode);
}

public class FileAccessPermissions : IFileAccessPermissions
{
  [SupportedOSPlatform("windows")]
  public void Set(
    string filePath,
    bool includeCurrentUser,
    bool isProtected,
    bool preserveInheritance,
    WellKnownSidType? owner,
    params WellKnownSidType[] allowedSids)
  {
    if (!OperatingSystem.IsWindows())
    {
      throw new PlatformNotSupportedException("WellKnownSidType permissions are only supported on Windows.");
    }
    using var currentUser = WindowsIdentity.GetCurrent();
    var fileInfo = new FileInfo(filePath);
    var security = preserveInheritance
      ? fileInfo.GetAccessControl()
      : new FileSecurity();

    security.SetAccessRuleProtection(isProtected, preserveInheritance);
    
    if (owner.HasValue)
    {
      security.SetOwner(new SecurityIdentifier(owner.Value, null));
    }
    foreach (var sid in allowedSids)
    {
      security.AddAccessRule(new FileSystemAccessRule(
        new SecurityIdentifier(sid, null),
        FileSystemRights.FullControl,
        AccessControlType.Allow));
    }
    if (includeCurrentUser && currentUser.User is not null)
    {
      security.AddAccessRule(new FileSystemAccessRule(
        currentUser.User,
        FileSystemRights.FullControl,
        AccessControlType.Allow));
    }
    fileInfo.SetAccessControl(security);
  }

  [SupportedOSPlatform("linux")]
  [SupportedOSPlatform("macos")]
  public void Set(string filePath, UnixFileMode mode)
  {
    if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
    {
      throw new PlatformNotSupportedException("Unix file permissions are only supported on Linux and macOS.");
    }

    File.SetUnixFileMode(filePath, mode);
  }
}
