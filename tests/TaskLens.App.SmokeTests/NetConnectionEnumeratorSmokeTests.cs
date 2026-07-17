using System.Net;
using System.Net.Sockets;
using TaskLens.App.Services;

namespace TaskLens.App.SmokeTests;

/// <summary>
/// Windows-only smoke tests for the GetExtendedTcpTable/GetExtendedUdpTable path (tm2r-03):
/// sockets opened by this process must come back under our own PID — no elevation involved.
/// </summary>
public class NetConnectionEnumeratorSmokeTests
{
    [Fact]
    public void OwnTcpListener_IsReportedAsAbhoeren()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var rows = NetConnectionEnumerator.Query([Environment.ProcessId]);

            var row = Assert.Single(rows, r => r.Protocol == "TCP" && r.LocalAddress == $"127.0.0.1:{port}");
            Assert.Equal("Abhören", row.State);
            Assert.Equal("—", row.RemoteAddress);
            Assert.Equal(Environment.ProcessId, row.Pid);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public void V6ListenerAndUdpSocket_GetTheirProtocolLabels()
    {
        var listener = new TcpListener(IPAddress.IPv6Loopback, 0);
        listener.Start();
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        try
        {
            var tcpPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            var udpPort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
            var rows = NetConnectionEnumerator.Query([Environment.ProcessId]);

            var tcp6 = Assert.Single(rows, r => r.Protocol == "TCPv6" && r.LocalPort == tcpPort);
            Assert.Equal($"[::1]:{tcpPort}", tcp6.LocalAddress);
            Assert.Equal("Abhören", tcp6.State);

            var udp4 = Assert.Single(rows, r => r.Protocol == "UDP" && r.LocalPort == udpPort);
            Assert.Equal("—", udp4.RemoteAddress);
        }
        finally
        {
            listener.Stop();
        }
    }
}
