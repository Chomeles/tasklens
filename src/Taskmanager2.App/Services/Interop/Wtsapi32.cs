using System.Runtime.InteropServices;

namespace Taskmanager2.App.Services.Interop;

/// <summary>wtsapi32 P/Invoke surface for <c>WtsUserSessionSource</c> — enumeration only, no mutation.</summary>
internal static partial class Wtsapi32
{
    /// <summary>WTS_INFO_CLASS.WTSUserName — the only info class this app queries.</summary>
    internal const int WtsUserName = 5;

    /// <summary>WTS_CONNECTSTATE_CLASS values that get German labels; the rest keep their enum name.</summary>
    internal const int WtsActive = 0;
    internal const int WtsDisconnected = 4;

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/ns-wtsapi32-wts_session_infow</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct WtsSessionInfo
    {
        internal uint SessionId;
        internal IntPtr WinStationName; // LPWSTR — unused, the name column is the user name
        internal int State;             // WTS_CONNECTSTATE_CLASS
    }

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsenumeratesessionsw
    /// Server handle IntPtr.Zero = WTS_CURRENT_SERVER_HANDLE; the buffer must go to WTSFreeMemory.
    /// </summary>
    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSEnumerateSessionsW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSEnumerateSessions(
        IntPtr server, uint reserved, uint version, out IntPtr sessionInfo, out uint count);

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsquerysessioninformationw
    /// The buffer must go to WTSFreeMemory.
    /// </summary>
    [LibraryImport("wtsapi32.dll", EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool WTSQuerySessionInformation(
        IntPtr server, uint sessionId, int infoClass, out IntPtr buffer, out uint bytesReturned);

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsfreememory</summary>
    [LibraryImport("wtsapi32.dll")]
    internal static partial void WTSFreeMemory(IntPtr memory);
}
