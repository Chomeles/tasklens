using System.Runtime.InteropServices;

namespace TaskLens.App.Services.Interop;

/// <summary>
/// pdh.dll P/Invoke surface for <c>PdhGpuProcessService</c>. Raw item arrays are read manually via
/// <see cref="Marshal"/> — no managed struct crosses the boundary except the fixed-size formatted
/// value record (research.md §3: reuse one long-lived query, never rebuild it per tick).
/// </summary>
internal static partial class Pdh
{
    internal const uint PdhFmtDouble = 0x00000200;
    internal const uint PdhMoreData = 0x800007D2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct FormattedCounterValueItem
    {
        public nint SzName;
        public uint CStatus;
        public double DoubleValue; // union member; only the double form is ever read (PdhFmtDouble)
    }

    [LibraryImport("pdh.dll", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint PdhOpenQuery(string? dataSource, nint userData, out nint query);

    [LibraryImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint PdhAddEnglishCounter(nint query, string counterPath, nint userData, out nint counter);

    [LibraryImport("pdh.dll")]
    internal static partial uint PdhCollectQueryData(nint query);

    [LibraryImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW")]
    internal static partial uint PdhGetFormattedCounterArray(
        nint counter, uint format, ref int bufferSize, ref int itemCount, nint itemBuffer);

    [StructLayout(LayoutKind.Sequential)]
    internal struct FormattedCounterValue
    {
        public uint Status;
        public double DoubleValue; // union member; only the double form is ever read (PdhFmtDouble)
    }

    [LibraryImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterValue")]
    internal static partial uint PdhGetFormattedCounterValue(
        nint counter, uint format, nint counterType, out FormattedCounterValue value);

    [LibraryImport("pdh.dll")]
    internal static partial uint PdhCloseQuery(nint query);
}
