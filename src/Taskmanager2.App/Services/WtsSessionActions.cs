using System.ComponentModel;
using System.Runtime.InteropServices;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

/// <summary>Verbindung trennen / Abmelden über WTSDisconnectSession/WTSLogoffSession (tm3-08).
/// Synchronous (wait = true) so the result is real; failures come back as data.</summary>
internal sealed class WtsSessionActions : ISessionActions
{
    public ActionResult Disconnect(int sessionId) =>
        WTSDisconnectSession(IntPtr.Zero, sessionId, true) ? ActionResult.Ok : Fail();

    public ActionResult Logoff(int sessionId) =>
        WTSLogoffSession(IntPtr.Zero, sessionId, true) ? ActionResult.Ok : Fail();

    private static ActionResult Fail() => ActionResult.Fail(new Win32Exception().Message);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSDisconnectSession(IntPtr server, int sessionId, bool wait);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSLogoffSession(IntPtr server, int sessionId, bool wait);
}
