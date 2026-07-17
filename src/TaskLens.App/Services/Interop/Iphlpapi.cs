using System.Runtime.InteropServices;

namespace TaskLens.App.Services.Interop;

/// <summary>
/// iphlpapi P/Invoke surface for <c>NetConnectionEnumerator</c> (tm2r-03): the per-process TCP/UDP
/// connection tables. Both calls work without elevation. Every table starts with
/// <c>DWORD dwNumEntries</c>, rows follow at offset 4 (all fields are 4-byte aligned). IPv6
/// <c>UCHAR[16]</c> addresses are declared as four DWORD chunks so the structs stay blittable —
/// identical memory layout. Ports sit in the low word of their DWORD in network byte order.
/// </summary>
internal static partial class Iphlpapi
{
    internal const uint AfInet = 2;
    internal const uint AfInet6 = 23;

    /// <summary><c>TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL</c>.</summary>
    internal const int TcpTableOwnerPidAll = 5;

    /// <summary><c>UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID</c>.</summary>
    internal const int UdpTableOwnerPid = 1;

    /// <summary>Win32: buffer too small, the size out-param holds the required byte count.</summary>
    internal const int ErrorInsufficientBuffer = 122;

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-getextendedtcptable</summary>
    [LibraryImport("iphlpapi.dll")]
    internal static partial int GetExtendedTcpTable(
        nint table,
        ref uint size,
        [MarshalAs(UnmanagedType.Bool)] bool sorted,
        uint addressFamily,
        int tableClass,
        uint reserved);

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-getextendedudptable</summary>
    [LibraryImport("iphlpapi.dll")]
    internal static partial int GetExtendedUdpTable(
        nint table,
        ref uint size,
        [MarshalAs(UnmanagedType.Bool)] bool sorted,
        uint addressFamily,
        int tableClass,
        uint reserved);

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/tcpmib/ns-tcpmib-mib_tcprow_owner_pid</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MibTcpRowOwnerPid
    {
        internal uint State;
        internal uint LocalAddr;
        internal uint LocalPort;
        internal uint RemoteAddr;
        internal uint RemotePort;
        internal uint OwningPid;
    }

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/tcpmib/ns-tcpmib-mib_tcp6row_owner_pid</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MibTcp6RowOwnerPid
    {
        internal uint LocalAddr0;
        internal uint LocalAddr1;
        internal uint LocalAddr2;
        internal uint LocalAddr3;
        internal uint LocalScopeId;
        internal uint LocalPort;
        internal uint RemoteAddr0;
        internal uint RemoteAddr1;
        internal uint RemoteAddr2;
        internal uint RemoteAddr3;
        internal uint RemoteScopeId;
        internal uint RemotePort;
        internal uint State;
        internal uint OwningPid;
    }

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/udpmib/ns-udpmib-mib_udprow_owner_pid</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MibUdpRowOwnerPid
    {
        internal uint LocalAddr;
        internal uint LocalPort;
        internal uint OwningPid;
    }

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/udpmib/ns-udpmib-mib_udp6row_owner_pid</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MibUdp6RowOwnerPid
    {
        internal uint LocalAddr0;
        internal uint LocalAddr1;
        internal uint LocalAddr2;
        internal uint LocalAddr3;
        internal uint LocalScopeId;
        internal uint LocalPort;
        internal uint OwningPid;
    }
}
