using System.Runtime.InteropServices;

namespace ControlR.Libraries.DevicesNative.Windows;

/// <summary>
/// contains information about the current state of both physical and virtual memory, including extended memory
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct MemoryStatusEx
{

    /// <summary>
    /// Initializes a new instance of the <see cref="T:MEMORYSTATUSEX"/> class.
    /// </summary>
    public MemoryStatusEx()
    {
        dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
    }

    /// <summary>
    /// Size of the structure, in bytes. You must set this member before calling GlobalMemoryStatusEx.
    /// </summary>
    public uint dwLength = 0;

    /// <summary>
    /// Number between 0 and 100 that specifies the approximate percentage of physical memory that is in use (0 indicates no memory use and 100 indicates full memory use).
    /// </summary>
    public uint dwMemoryLoad = 0;

    /// <summary>
    /// Total size of physical memory, in bytes.
    /// </summary>
    public ulong ullTotalPhys = 0;

    /// <summary>
    /// Size of physical memory available, in bytes.
    /// </summary>
    public ulong ullAvailPhys = 0;

    /// <summary>
    /// Size of the committed memory limit, in bytes. This is physical memory plus the size of the page file, minus a small overhead.
    /// </summary>
    public ulong ullTotalPageFile = 0;

    /// <summary>
    /// Size of available memory to commit, in bytes. The limit is ullTotalPageFile.
    /// </summary>
    public ulong ullAvailPageFile = 0;

    /// <summary>
    /// Total size of the user mode portion of the virtual address space of the calling process, in bytes.
    /// </summary>
    public ulong ullTotalVirtual = 0;

    /// <summary>
    /// Size of unreserved and uncommitted memory in the user mode portion of the virtual address space of the calling process, in bytes.
    /// </summary>
    public ulong ullAvailVirtual = 0;

    /// <summary>
    /// Size of unreserved and uncommitted memory in the extended portion of the virtual address space of the calling process, in bytes.
    /// </summary>
    public ulong ullAvailExtendedVirtual = 0;

}
