using System.Net;
using System.Net.Sockets;
using TaskLens.App.Services;
using TaskLens.Core.Services;

namespace TaskLens.App.SmokeTests;

/// <summary>
/// Windows-only smoke test for the real ETW kernel-network path: a loopback TCP transfer must be
/// attributed to our own PID. ETW real-time sessions require elevation — CI runners are admin and
/// take the real path; an unelevated local run reports RequiresAdmin and the test skips instead of
/// failing. Rate math and pruning are unit-tested on Linux in Core's ProcessNetworkAggregator.
/// </summary>
public class EtwProcessNetworkServiceSmokeTests
{
    [Fact]
    public void LoopbackTransfer_AttributesBytesToOwnPid()
    {
        using var service = new EtwProcessNetworkService();
        if (service.Availability == NetworkAttributionAvailability.RequiresAdmin)
        {
            return; // unelevated local run — nothing to assert without a session
        }

        Assert.Equal(NetworkAttributionAvailability.Ok, service.Availability);
        _ = service.SampleNetworkBytesPerSecondByPid(); // prime the rate window

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var payload = new byte[1024 * 1024];
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                using (var client = new TcpClient())
                {
                    client.Connect(IPAddress.Loopback, port);
                    using var server = listener.AcceptTcpClient();
                    // Write on a background task: 1 MB exceeds the loopback in-flight capacity
                    // (~128 KB of default socket buffers), so a synchronous Write before the
                    // read loop below would deadlock this thread forever.
                    var clientStream = client.GetStream();
                    var send = Task.Run(() => clientStream.Write(payload, 0, payload.Length));

                    var stream = server.GetStream();
                    var buffer = new byte[64 * 1024];
                    var remaining = payload.Length;
                    while (remaining > 0)
                    {
                        var read = stream.Read(buffer, 0, buffer.Length);
                        Assert.True(read > 0, "loopback connection closed before the payload arrived");
                        remaining -= read;
                    }

                    send.GetAwaiter().GetResult();
                }

                Thread.Sleep(500); // give the session's flush timer a chance to deliver buffers
                var rates = service.SampleNetworkBytesPerSecondByPid();
                if (rates.TryGetValue(Environment.ProcessId, out var rate) && rate > 0)
                {
                    return; // our own transfer showed up — the whole path works
                }
            }

            Assert.Fail("own PID never reported a network rate within the deadline");
        }
        finally
        {
            listener.Stop();
        }
    }
}
