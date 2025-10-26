using System.Runtime.InteropServices;

  #pragma warning disable IDE0079 // Remove unnecessary suppression
  #pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
  #pragma warning disable CA1401 // P/Invokes should not be visible

  namespace ControlR.Libraries.NativeInterop.Unix;

public static class Libc
{
  [DllImport("libc", EntryPoint = "geteuid", SetLastError = true)]
  public static extern uint Geteuid();

  [DllImport("libc", EntryPoint = "setsid", SetLastError = true)]
  public static extern int Setsid();

  [DllImport("libc", EntryPoint = "umask", SetLastError = true)]
  public static extern int Umask(int mask);
  
  [DllImport("libc", EntryPoint = "chown", SetLastError = true, CharSet = CharSet.Ansi)]
  public static extern int chown([MarshalAs(UnmanagedType.LPStr)] string path, int owner, int group);

  [DllImport("libc", EntryPoint = "getgrgid", SetLastError = true)]
  public static extern IntPtr getgrgid(uint gid);

  [DllImport("libc", EntryPoint = "getgrnam", SetLastError = true, CharSet = CharSet.Ansi)]
  public static extern IntPtr getgrnam([MarshalAs(UnmanagedType.LPStr)] string name);

  [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
  public static extern int stat(string path, out Stat buf);

  [StructLayout(LayoutKind.Sequential)]
  public struct Group
  {
    public IntPtr gr_name;
    public IntPtr gr_passwd;
    public uint gr_gid;
    public IntPtr gr_mem;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct Stat
  {
    public uint st_dev;
    public ushort st_mode;
    public ushort st_nlink;
    public ulong st_ino;
    public uint st_uid;
    public uint st_gid;
    public uint st_rdev;
    public long st_atimespec_sec;
    public long st_atimespec_nsec;
    public long st_mtimespec_sec;
    public long st_mtimespec_nsec;
    public long st_ctimespec_sec;
    public long st_ctimespec_nsec;
    public long st_birthtimespec_sec;
    public long st_birthtimespec_nsec;
    public long st_size;
    public long st_blocks;
    public int st_blksize;
    public uint st_flags;
    public uint st_gen;
    public int st_lspare;
    public long st_qspare0;
    public long st_qspare1;
  }
}