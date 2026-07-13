using System.Runtime.InteropServices;

namespace Taskmanager2.App.Services.Interop;

/// <summary>advapi32 P/Invoke surface for <c>ScmServiceCatalog</c> — query-only, no mutation.</summary>
internal static partial class Advapi32
{
    /// <summary>SC_STATUS_PROCESS_INFO — the only defined info level of QueryServiceStatusEx.</summary>
    internal const int ScStatusProcessInfo = 0;

    /// <summary>SERVICE_CONFIG_DESCRIPTION info level of QueryServiceConfig2.</summary>
    internal const int ServiceConfigDescription = 1;

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/winsvc/ns-winsvc-service_status_process</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct ServiceStatusProcess
    {
        internal uint ServiceType;
        internal uint CurrentState;
        internal uint ControlsAccepted;
        internal uint Win32ExitCode;
        internal uint ServiceSpecificExitCode;
        internal uint CheckPoint;
        internal uint WaitHint;
        internal uint ProcessId;
        internal uint ServiceFlags;
    }

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-queryservicestatusex</summary>
    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool QueryServiceStatusEx(
        SafeHandle serviceHandle, int infoLevel, out ServiceStatusProcess buffer, int bufferSize, out int bytesNeeded);

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-queryserviceconfig2w
    /// For SERVICE_CONFIG_DESCRIPTION the buffer receives a SERVICE_DESCRIPTIONW: one LPWSTR at
    /// offset 0 (possibly NULL), with the string data behind it in the same buffer.
    /// </summary>
    [LibraryImport("advapi32.dll", EntryPoint = "QueryServiceConfig2W", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool QueryServiceConfig2(
        SafeHandle serviceHandle, int infoLevel, IntPtr buffer, int bufferSize, out int bytesNeeded);
}
