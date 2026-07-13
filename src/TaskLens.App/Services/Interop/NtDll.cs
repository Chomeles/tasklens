using System.Runtime.InteropServices;

namespace TaskLens.App.Services.Interop;

/// <summary>
/// ntdll P/Invoke surface for <c>NtProcessEnumerator</c>. No native structs are declared here —
/// the buffer is parsed byte-wise by <c>SystemProcessInformationParser</c> in Core (research.md §3).
/// </summary>
internal static partial class NtDll
{
    /// <summary><c>SYSTEM_INFORMATION_CLASS.SystemProcessInformation</c>.</summary>
    internal const int SystemProcessInformation = 5;

    /// <summary>NTSTATUS: buffer too small, <c>returnLength</c> holds the required size.</summary>
    internal const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntquerysysteminformation</summary>
    [LibraryImport("ntdll.dll")]
    internal static partial int NtQuerySystemInformation(
        int systemInformationClass,
        nint systemInformation,
        uint systemInformationLength,
        out uint returnLength);
}
