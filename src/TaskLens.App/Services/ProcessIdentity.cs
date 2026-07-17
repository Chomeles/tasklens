using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace TaskLens.App.Services;

/// <summary>
/// Per-process owner ("DESKTOP\Orkan", token user SID → account name), architecture ("x64",
/// "x86", "ARM64") and command line for the Details page — the real Task Manager's
/// Benutzername/Architektur/Befehlszeile columns. All are immutable for a process lifetime, so
/// lookups are cached by PID; processes we cannot open (system-protected) yield nulls and the
/// cells render as em-dashes (command line: empty), never invented.
/// </summary>
internal static class ProcessIdentity
{
    private static readonly ConcurrentDictionary<int, (string? User, string? Architecture, string? CommandLine)> Cache = new();

    internal static (string? User, string? Architecture, string? CommandLine) Lookup(int pid) => Cache.GetOrAdd(pid, static p =>
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, p);
        if (handle == IntPtr.Zero)
        {
            return (null, null, null);
        }

        try
        {
            return (TryGetUser(handle), TryGetArchitecture(handle), TryGetCommandLine(handle));
        }
        finally
        {
            CloseHandle(handle);
        }
    });

    /// <summary>Drops cache entries for PIDs no longer alive — PID reuse would serve stale identity.</summary>
    internal static void Prune(IReadOnlySet<int> livePids)
    {
        foreach (var pid in Cache.Keys.Where(k => !livePids.Contains(k)).ToList())
        {
            Cache.TryRemove(pid, out _);
        }
    }

    private static string? TryGetUser(IntPtr process)
    {
        if (!OpenProcessToken(process, TokenQuery, out var token))
        {
            return null;
        }

        try
        {
            GetTokenInformation(token, TokenUserClass, IntPtr.Zero, 0, out var size);
            if (size == 0)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (!GetTokenInformation(token, TokenUserClass, buffer, size, out _))
                {
                    return null;
                }

                var sid = Marshal.ReadIntPtr(buffer); // TOKEN_USER.User.Sid
                var name = new char[256];
                var domain = new char[256];
                int nameLen = name.Length, domainLen = domain.Length;
                return LookupAccountSid(null, sid, name, ref nameLen, domain, ref domainLen, out _)
                    ? new string(name, 0, nameLen)
                    : null;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static string? TryGetArchitecture(IntPtr process)
    {
        if (!IsWow64Process2(process, out var processMachine, out var nativeMachine))
        {
            return null;
        }

        // IMAGE_FILE_MACHINE_UNKNOWN as process machine = not WOW64 → the native architecture.
        var machine = processMachine == 0 ? nativeMachine : processMachine;
        return machine switch
        {
            0x8664 => "x64",
            0x014c => "x86",
            0xAA64 => "ARM64",
            _ => null,
        };
    }

    private static string? TryGetCommandLine(IntPtr process)
    {
        // Two-stage: size probe answers STATUS_INFO_LENGTH_MISMATCH with the required length, then
        // the fetch fills a UNICODE_STRING header whose Buffer points at the chars right after it.
        // PROCESS_QUERY_LIMITED_INFORMATION suffices for class 60 (Win 8.1+).
        var status = NtQueryInformationProcess(process, ProcessCommandLineInformation, IntPtr.Zero, 0, out var size);
        if (status != Interop.NtDll.StatusInfoLengthMismatch || size == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (NtQueryInformationProcess(process, ProcessCommandLineInformation, buffer, size, out _) != 0)
            {
                return null;
            }

            var lengthBytes = (ushort)Marshal.ReadInt16(buffer);  // UNICODE_STRING.Length
            var chars = Marshal.ReadIntPtr(buffer, IntPtr.Size);  // UNICODE_STRING.Buffer
            return chars == IntPtr.Zero ? null : Marshal.PtrToStringUni(chars, lengthBytes / 2);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private const int ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessCommandLineInformation = 60;
    private const uint TokenQuery = 0x0008;
    private const int TokenUserClass = 1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process2(IntPtr process, out ushort processMachine, out ushort nativeMachine);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint desiredAccess, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, uint length, out uint returnLength);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupAccountSid(string? system, IntPtr sid, char[] name, ref int nameLength, char[] domain, ref int domainLength, out int use);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr process, int infoClass, IntPtr info, uint length, out uint returnLength);
}
