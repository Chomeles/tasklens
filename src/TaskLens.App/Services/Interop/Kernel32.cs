using System.Runtime.InteropServices;

namespace TaskLens.App.Services.Interop;

/// <summary>kernel32 P/Invoke surface for <c>WinSystemMetricsService</c>.</summary>
internal static partial class Kernel32
{
    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/ns-sysinfoapi-memorystatusex</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatusEx
    {
        internal uint Length;
        internal uint MemoryLoad;
        internal ulong TotalPhys;
        internal ulong AvailPhys;
        internal ulong TotalPageFile;
        internal ulong AvailPageFile;
        internal ulong TotalVirtual;
        internal ulong AvailVirtual;
        internal ulong AvailExtendedVirtual;
    }

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex</summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getsystemtimes
    /// The FILETIME out-params are received as raw 64-bit tick counts (low+high dword).
    /// </summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);

    /// <summary>
    /// https://learn.microsoft.com/en-us/windows/win32/api/psapi/ns-psapi-performance_information
    /// Size fields are in pages; multiply by <see cref="PageSize"/> for bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PerformanceInformation
    {
        internal uint Cb;
        internal nuint CommitTotal;
        internal nuint CommitLimit;
        internal nuint CommitPeak;
        internal nuint PhysicalTotal;
        internal nuint PhysicalAvailable;
        internal nuint SystemCache;
        internal nuint KernelTotal;
        internal nuint KernelPaged;
        internal nuint KernelNonpaged;
        internal nuint PageSize;
        internal uint HandleCount;
        internal uint ProcessCount;
        internal uint ThreadCount;
    }

    /// <summary>https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getperformanceinfo (kernel32 export)</summary>
    [LibraryImport("kernel32.dll", EntryPoint = "K32GetPerformanceInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetPerformanceInfo(ref PerformanceInformation info, uint size);
}
