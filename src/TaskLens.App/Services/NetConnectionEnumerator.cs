using System.Buffers.Binary;
using System.Net;
using System.Runtime.InteropServices;
using TaskLens.App.Services.Interop;

namespace TaskLens.App.Services;

/// <summary>One open connection, preformatted for the „Netzwerkverbindungen" dialog (tm2r-03).</summary>
internal sealed record NetConnectionRow(
    string Protocol,
    string LocalAddress,
    string RemoteAddress,
    string State,
    int LocalPort,
    int Pid);

/// <summary>
/// TCPView-replacement data source: a process's open TCP/UDP endpoints via
/// <c>GetExtendedTcpTable</c>/<c>GetExtendedUdpTable</c> (v4 + v6, no elevation needed). Rows are
/// filtered to the given PIDs and sorted Protokoll → lokaler Port. Remote is „—" for listeners and
/// UDP; TCP states use the German netstat wording (Abhören/Hergestellt), other states keep their
/// raw MIB name — same policy as the real netstat output the dialog imitates.
/// </summary>
internal static class NetConnectionEnumerator
{
    private const uint TcpStateListen = 2;
    private const uint TcpStateEstablished = 5;

    internal static IReadOnlyList<NetConnectionRow> Query(IReadOnlyCollection<int> pids)
    {
        ArgumentNullException.ThrowIfNull(pids);
        var wanted = new HashSet<int>(pids);
        var rows = new List<NetConnectionRow>();
        Append(rows, Tcp4(wanted));
        Append(rows, Tcp6(wanted));
        Append(rows, Udp4(wanted));
        Append(rows, Udp6(wanted));
        return rows;
    }

    /// <summary>Appends one protocol's block sorted by local port (Protokoll → lokaler Port).</summary>
    private static void Append(List<NetConnectionRow> rows, List<NetConnectionRow> block)
    {
        block.Sort((a, b) => a.LocalPort.CompareTo(b.LocalPort));
        rows.AddRange(block);
    }

    private static List<NetConnectionRow> Tcp4(HashSet<int> pids)
    {
        var rows = new List<NetConnectionRow>();
        foreach (var row in Rows<Iphlpapi.MibTcpRowOwnerPid>(tcp: true, Iphlpapi.AfInet))
        {
            if (pids.Contains((int)row.OwningPid))
            {
                var localPort = Port(row.LocalPort);
                rows.Add(new NetConnectionRow(
                    "TCP",
                    V4(row.LocalAddr, localPort),
                    row.State == TcpStateListen ? "—" : V4(row.RemoteAddr, Port(row.RemotePort)),
                    StateName(row.State),
                    localPort,
                    (int)row.OwningPid));
            }
        }

        return rows;
    }

    private static List<NetConnectionRow> Tcp6(HashSet<int> pids)
    {
        var rows = new List<NetConnectionRow>();
        foreach (var row in Rows<Iphlpapi.MibTcp6RowOwnerPid>(tcp: true, Iphlpapi.AfInet6))
        {
            if (pids.Contains((int)row.OwningPid))
            {
                var localPort = Port(row.LocalPort);
                rows.Add(new NetConnectionRow(
                    "TCPv6",
                    V6(row.LocalAddr0, row.LocalAddr1, row.LocalAddr2, row.LocalAddr3, row.LocalScopeId, localPort),
                    row.State == TcpStateListen
                        ? "—"
                        : V6(row.RemoteAddr0, row.RemoteAddr1, row.RemoteAddr2, row.RemoteAddr3, row.RemoteScopeId, Port(row.RemotePort)),
                    StateName(row.State),
                    localPort,
                    (int)row.OwningPid));
            }
        }

        return rows;
    }

    private static List<NetConnectionRow> Udp4(HashSet<int> pids)
    {
        var rows = new List<NetConnectionRow>();
        foreach (var row in Rows<Iphlpapi.MibUdpRowOwnerPid>(tcp: false, Iphlpapi.AfInet))
        {
            if (pids.Contains((int)row.OwningPid))
            {
                var localPort = Port(row.LocalPort);
                rows.Add(new NetConnectionRow(
                    "UDP", V4(row.LocalAddr, localPort), "—", "", localPort, (int)row.OwningPid));
            }
        }

        return rows;
    }

    private static List<NetConnectionRow> Udp6(HashSet<int> pids)
    {
        var rows = new List<NetConnectionRow>();
        foreach (var row in Rows<Iphlpapi.MibUdp6RowOwnerPid>(tcp: false, Iphlpapi.AfInet6))
        {
            if (pids.Contains((int)row.OwningPid))
            {
                var localPort = Port(row.LocalPort);
                rows.Add(new NetConnectionRow(
                    "UDPv6",
                    V6(row.LocalAddr0, row.LocalAddr1, row.LocalAddr2, row.LocalAddr3, row.LocalScopeId, localPort),
                    "—",
                    "",
                    localPort,
                    (int)row.OwningPid));
            }
        }

        return rows;
    }

    /// <summary>Reads one table's rows: <c>dwNumEntries</c> at offset 0, packed rows from offset 4.</summary>
    private static IEnumerable<T> Rows<T>(bool tcp, uint family)
        where T : struct
    {
        var table = Fetch(tcp, family);
        if (table is null || table.Length < 4)
        {
            yield break;
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(table);
        var stride = Marshal.SizeOf<T>();
        for (var i = 0; i < count; i++)
        {
            var offset = 4 + (i * stride);
            if (offset + stride > table.Length)
            {
                yield break; // dwNumEntries lied — trust the buffer bounds
            }

            yield return MemoryMarshal.Read<T>(table.AsSpan(offset, stride));
        }
    }

    /// <summary>Two-stage buffer: probe the size, then fetch; null when the table is unavailable.</summary>
    private static byte[]? Fetch(bool tcp, uint family)
    {
        uint size = 0;

        // The table can grow between probe and fetch — the failed fetch reports the new size, retry.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var buffer = size == 0 ? nint.Zero : Marshal.AllocHGlobal((int)size);
            try
            {
                var error = tcp
                    ? Iphlpapi.GetExtendedTcpTable(buffer, ref size, sorted: true, family, Iphlpapi.TcpTableOwnerPidAll, 0)
                    : Iphlpapi.GetExtendedUdpTable(buffer, ref size, sorted: true, family, Iphlpapi.UdpTableOwnerPid, 0);
                if (error == 0 && buffer != nint.Zero)
                {
                    var bytes = new byte[size];
                    Marshal.Copy(buffer, bytes, 0, (int)size);
                    return bytes;
                }

                if (error != Iphlpapi.ErrorInsufficientBuffer)
                {
                    return null; // family not available (no v6 stack) or other hard failure — honest empty
                }
            }
            finally
            {
                if (buffer != nint.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        return null;
    }

    /// <summary>Ports sit in the low word of their DWORD in network byte order (see MIB docs).</summary>
    private static ushort Port(uint networkOrderPort) => BinaryPrimitives.ReverseEndianness((ushort)networkOrderPort);

    private static string V4(uint networkOrderAddr, ushort port) =>
        new IPEndPoint(new IPAddress(networkOrderAddr), port).ToString();

    private static string V6(uint addr0, uint addr1, uint addr2, uint addr3, uint scopeId, ushort port)
    {
        // The four DWORD chunks are the UCHAR[16] address in memory order; Windows is little-endian.
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, addr0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], addr1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..], addr2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[12..], addr3);
        return new IPEndPoint(new IPAddress(bytes, scopeId), port).ToString();
    }

    /// <summary>MIB_TCP_STATE → Anzeige: deutsche netstat-Begriffe für die zwei Alltagszustände, sonst Rohname.</summary>
    private static string StateName(uint state) => state switch
    {
        1 => "CLOSED",
        TcpStateListen => "Abhören",
        3 => "SYN_SENT",
        4 => "SYN_RCVD",
        TcpStateEstablished => "Hergestellt",
        6 => "FIN_WAIT1",
        7 => "FIN_WAIT2",
        8 => "CLOSE_WAIT",
        9 => "CLOSING",
        10 => "LAST_ACK",
        11 => "TIME_WAIT",
        12 => "DELETE_TCB",
        _ => state.ToString(),
    };
}
