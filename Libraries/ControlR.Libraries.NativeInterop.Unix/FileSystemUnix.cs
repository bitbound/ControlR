
using System.Runtime.InteropServices;

namespace ControlR.Libraries.NativeInterop.Unix;


public interface IFileSystemUnix
{
	/// <summary>
	/// Gets the group name of the file at the specified path, or null if unavailable.
	/// </summary>
	string? GetFileGroup(string path);

	/// <summary>
	/// Sets the group of the file at the specified path. Returns true on success.
	/// </summary>
	bool SetFileGroup(string path, string groupName);
}

public class FileSystemUnix : IFileSystemUnix
{
	public string? GetFileGroup(string path)
	{
		if (string.IsNullOrEmpty(path))
			return null;

		if (Libc.stat(path, out var statBuf) != 0)
			return null;

		var grpPtr = Libc.getgrgid(statBuf.st_gid);
		if (grpPtr == IntPtr.Zero)
			return null;

		var group = Marshal.PtrToStructure<Libc.Group>(grpPtr);
		return Marshal.PtrToStringAnsi(group.gr_name);
	}

	public bool SetFileGroup(string path, string groupName)
	{
		if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(groupName))
			return false;

		var grpPtr = Libc.getgrnam(groupName);
		if (grpPtr == IntPtr.Zero)
			return false;

		var group = Marshal.PtrToStructure<Libc.Group>(grpPtr);
		int result = Libc.chown(path, -1, (int)group.gr_gid);
		return result == 0;
	}
}

