using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Taskmanager2.App.Services.Interop;

/// <summary>SCM or service handle, closed via <c>CloseServiceHandle</c>.</summary>
internal sealed class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeServiceHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle() => Advapi32.CloseServiceHandle(handle);
}

/// <summary>advapi32 P/Invoke surface for <c>ScmServiceCatalog</c> — query-only, no mutation.</summary>
internal static partial class Advapi32
{
    /// <summary>SC_STATUS_PROCESS_INFO — the only defined info level of QueryServiceStatusEx.</summary>
    internal const int ScStatusProcessInfo = 0;

    /// <summary>SERVICE_CONFIG_DESCRIPTION info level of QueryServiceConfig2.</summary>
    internal const int ServiceConfigDescription = 1;

    /// <summary>SC_MANAGER_CONNECT — the only SCM right needed to open services.</summary>
    internal const int ScManagerConnect = 0x0001;

    /// <summary>
    /// SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS — strictly read-only rights, so SDDL-hardened
    /// services (WinDefend …) answer too; ServiceController.ServiceHandle would demand
    /// SERVICE_ALL_ACCESS and be denied there even elevated.
    /// </summary>
    internal const int ServiceQueryRights = 0x0001 | 0x0004;

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-openscmanagerw</summary>
    [LibraryImport("advapi32.dll", EntryPoint = "OpenSCManagerW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial SafeServiceHandle OpenSCManager(string? machineName, string? databaseName, int desiredAccess);

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-openservicew</summary>
    [LibraryImport("advapi32.dll", EntryPoint = "OpenServiceW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial SafeServiceHandle OpenService(SafeHandle scmHandle, string serviceName, int desiredAccess);

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-closeservicehandle</summary>
    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseServiceHandle(IntPtr handle);

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
