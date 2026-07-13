using System.Runtime.InteropServices;
using TaskLens.Core.Models;
using TaskLens.Core.Services;
using Taskmanager2.App.Services.Interop;

namespace Taskmanager2.App.Services;

/// <summary>
/// Read-only <see cref="IUserSessionSource"/> over <c>WTSEnumerateSessionsW</c> on the local
/// server handle (no extra rights needed). Sessions without a user name — services session,
/// listeners — are skipped, like in the real Task Manager. A failed enumeration degrades to
/// <see cref="CatalogAvailability.AccessDenied"/>; a per-session query failure just skips that
/// session. No disconnect/logoff API anywhere (plan-tm2.md §2).
/// </summary>
internal sealed class WtsUserSessionSource : IUserSessionSource
{
    public UserSessionSnapshot Query()
    {
        // IntPtr.Zero = WTS_CURRENT_SERVER_HANDLE, reserved 0, version 1 (the only defined one).
        if (!Wtsapi32.WTSEnumerateSessions(IntPtr.Zero, 0, 1, out var info, out var count))
        {
            return new UserSessionSnapshot([], CatalogAvailability.AccessDenied);
        }

        try
        {
            var sessions = new List<UserSession>();
            var stride = Marshal.SizeOf<Wtsapi32.WtsSessionInfo>();
            for (var i = 0; i < count; i++)
            {
                var session = Marshal.PtrToStructure<Wtsapi32.WtsSessionInfo>(IntPtr.Add(info, i * stride));
                var userName = QueryUserName(session.SessionId);
                if (userName.Length == 0)
                {
                    continue; // services / listener sessions have no user — the real TM hides them too
                }

                sessions.Add(new UserSession((int)session.SessionId, userName, MapState(session.State)));
            }

            return new UserSessionSnapshot(sessions, CatalogAvailability.Available);
        }
        finally
        {
            Wtsapi32.WTSFreeMemory(info);
        }
    }

    private static string QueryUserName(uint sessionId)
    {
        if (!Wtsapi32.WTSQuerySessionInformation(IntPtr.Zero, sessionId, Wtsapi32.WtsUserName, out var buffer, out _))
        {
            return "";
        }

        try
        {
            return Marshal.PtrToStringUni(buffer) ?? "";
        }
        finally
        {
            Wtsapi32.WTSFreeMemory(buffer);
        }
    }

    /// <summary>WTS_CONNECTSTATE_CLASS → German where the Task Manager has a word, enum name otherwise.</summary>
    private static string MapState(int state) => state switch
    {
        Wtsapi32.WtsActive => "Aktiv",
        Wtsapi32.WtsDisconnected => "Getrennt",
        1 => "Connected",
        2 => "ConnectQuery",
        3 => "Shadow",
        5 => "Idle",
        6 => "Listen",
        7 => "Reset",
        8 => "Down",
        9 => "Init",
        _ => state.ToString(),
    };
}
