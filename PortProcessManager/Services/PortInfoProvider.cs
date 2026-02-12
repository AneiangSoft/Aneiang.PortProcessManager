using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PortProcessManager.Services;

public enum ProtocolType { TCP, UDP }

public enum RowChangeState { None, New, Changed }

public partial class PortRow : ObservableObject
{
    [ObservableProperty]
    private ProtocolType protocol;

    [ObservableProperty]
    private string localAddress = string.Empty;

    [ObservableProperty]
    private int localPort;

    [ObservableProperty]
    private string remoteAddress = string.Empty;

    [ObservableProperty]
    private int remotePort;

    [ObservableProperty]
    private string state = string.Empty;

    [ObservableProperty]
    private int pid;

    [ObservableProperty]
    private string processName = "Unknown";

    [ObservableProperty]
    private string processPath = string.Empty;

    [ObservableProperty]
    private string owner = "Unknown";

    [ObservableProperty]
    private ImageSource? icon;

    [ObservableProperty]
    private RowChangeState changeState = RowChangeState.None;

    public string GroupKey => string.IsNullOrEmpty(ProcessPath) || ProcessPath.StartsWith('[') 
        ? ProcessName 
        : $"{ProcessName} ({ProcessPath})";

    public string UniqueKey => $"{Protocol}-{LocalAddress}-{LocalPort}-{RemoteAddress}-{RemotePort}-{Pid}";
}

public static class PortInfoProvider
{
    private static readonly HashSet<string> ProtectedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "System Idle Process",
        "csrss",
        "lsass",
        "smss",
        "wininit",
        "services",
        "winlogon"
    };

    public static bool IsProtected(PortRow row)
    {
        if (row.Pid <= 4) return true;
        return ProtectedProcessNames.Contains(row.ProcessName);
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved = 0);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved = 0);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;
    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;
    private const int UDP_TABLE_OWNER_PID = 1;
    private const uint TOKEN_QUERY = 0x0008;

    private static readonly Dictionary<string, ImageSource?> IconCache = new();

    public static List<PortRow> GetPortProcesses()
    {
        var results = new List<PortRow>();
        results.AddRange(GetTcpConnections());
        results.AddRange(GetUdpConnections());

        var processCache = new Dictionary<int, (string Name, string Path, string Owner, ImageSource? Icon)>();

        foreach (var row in results)
        {
            if (row.Pid < 0) continue;

            if (processCache.TryGetValue(row.Pid, out var info))
            {
                row.ProcessName = info.Name;
                row.ProcessPath = info.Path;
                row.Owner = info.Owner;
                row.Icon = info.Icon;
            }
            else
            {
                try
                {
                    if (row.Pid == 0)
                    {
                        row.ProcessName = "System Idle Process";
                        row.ProcessPath = string.Empty;
                        row.Owner = "SYSTEM";
                    }
                    else if (row.Pid == 4)
                    {
                        row.ProcessName = "System";
                        row.ProcessPath = "ntoskrnl.exe";
                        row.Owner = "SYSTEM";
                    }
                    else
                    {
                        using var proc = Process.GetProcessById(row.Pid);
                        row.ProcessName = proc.ProcessName;
                        row.Owner = GetProcessOwner(proc);
                        try
                        {
                            row.ProcessPath = proc.MainModule?.FileName ?? string.Empty;
                        }
                        catch
                        {
                            row.ProcessPath = "[Access Denied]";
                        }
                    }
                    row.Icon = GetIcon(row.ProcessPath);
                }
                catch
                {
                    row.ProcessName = "Unknown (Exited)";
                    row.ProcessPath = string.Empty;
                    row.Owner = "Unknown";
                    row.Icon = null;
                }
                processCache[row.Pid] = (row.ProcessName, row.ProcessPath, row.Owner, row.Icon);
            }
        }

        return results;
    }

    private static ImageSource? GetIcon(string path)
    {
        if (string.IsNullOrEmpty(path) || path.StartsWith('[')) return null;
        if (IconCache.TryGetValue(path, out var cachedIcon)) return cachedIcon;

        try
        {
            SHFILEINFO shfi = new SHFILEINFO();
            IntPtr hIcon = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_SMALLICON);

            if (hIcon != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
            {
                var icon = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                icon.Freeze();
                IconCache[path] = icon;
                return icon;
            }
        }
        catch { }
        return null;
    }

    private static string GetProcessOwner(Process process)
    {
        IntPtr tokenHandle = IntPtr.Zero;
        try
        {
            if (OpenProcessToken(process.Handle, TOKEN_QUERY, out tokenHandle))
            {
                using var identity = new WindowsIdentity(tokenHandle);
                return identity.Name;
            }
        }
        catch { }
        finally
        {
            if (tokenHandle != IntPtr.Zero) CloseHandle(tokenHandle);
        }
        return "[Access Denied]";
    }

    private static List<PortRow> GetTcpConnections()
    {
        var list = new List<PortRow>();
        int size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL);

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, false, AF_INET, TCP_TABLE_OWNER_PID_ALL) == 0)
            {
                int count = Marshal.ReadInt32(buffer);
                IntPtr ptr = IntPtr.Add(buffer, 4);
                for (int i = 0; i < count; i++)
                {
                    var data = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(ptr);
                    list.Add(new PortRow
                    {
                        Protocol = ProtocolType.TCP,
                        LocalAddress = new IPAddress(data.dwLocalAddr).ToString(),
                        LocalPort = unchecked((int)(((data.dwLocalPort & 0xff) << 8) | ((data.dwLocalPort & 0xff00) >> 8))),
                        RemoteAddress = new IPAddress(data.dwRemoteAddr).ToString(),
                        RemotePort = unchecked((int)(((data.dwRemotePort & 0xff) << 8) | ((data.dwRemotePort & 0xff00) >> 8))),
                        Pid = data.dwOwningPid,
                        State = ((TcpState)data.dwState).ToString()
                    });
                    ptr = IntPtr.Add(ptr, Marshal.SizeOf<MIB_TCPROW_OWNER_PID>());
                }
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return list;
    }

    private static List<PortRow> GetUdpConnections()
    {
        var list = new List<PortRow>();
        int size = 0;
        GetExtendedUdpTable(IntPtr.Zero, ref size, false, AF_INET, UDP_TABLE_OWNER_PID);

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedUdpTable(buffer, ref size, false, AF_INET, UDP_TABLE_OWNER_PID) == 0)
            {
                int count = Marshal.ReadInt32(buffer);
                IntPtr ptr = IntPtr.Add(buffer, 4);
                for (int i = 0; i < count; i++)
                {
                    var data = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(ptr);
                    list.Add(new PortRow
                    {
                        Protocol = ProtocolType.UDP,
                        LocalAddress = new IPAddress(data.dwLocalAddr).ToString(),
                        LocalPort = unchecked((int)(((data.dwLocalPort & 0xff) << 8) | ((data.dwLocalPort & 0xff00) >> 8))),
                        Pid = data.dwOwningPid,
                        State = "-"
                    });
                    ptr = IntPtr.Add(ptr, Marshal.SizeOf<MIB_UDPROW_OWNER_PID>());
                }
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
        return list;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint dwState;
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public uint dwRemoteAddr;
        public uint dwRemotePort;
        public int dwOwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW_OWNER_PID
    {
        public uint dwLocalAddr;
        public uint dwLocalPort;
        public int dwOwningPid;
    }

    private enum TcpState : uint
    {
        CLOSED = 1, LISTEN = 2, SYN_SENT = 3, SYN_RCVD = 4, ESTABLISHED = 5,
        FIN_WAIT1 = 6, FIN_WAIT2 = 7, CLOSE_WAIT = 8, CLOSING = 9, LAST_ACK = 10,
        TIME_WAIT = 11, DELETE_TCB = 12
    }
}
