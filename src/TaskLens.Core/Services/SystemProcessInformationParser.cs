using System.Buffers.Binary;
using System.Text;
using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>
/// Pure parser for the 64-bit (x64/arm64) <c>SYSTEM_PROCESS_INFORMATION</c> buffer returned by
/// <c>NtQuerySystemInformation(SystemProcessInformation)</c>. Lives in Core so the byte-level
/// logic is unit-testable on Linux against byte fixtures; the P/Invoke that fills the buffer is
/// <c>NtProcessEnumerator</c> in the App project.
/// Layout source: https://ntdoc.m417z.com/ntquerysysteminformation (stable for ~20 years,
/// see research.md §3). Offsets are for 64-bit little-endian Windows only.
/// </summary>
public static class SystemProcessInformationParser
{
    // SYSTEM_PROCESS_INFORMATION field offsets, 64-bit layout.
    private const int NextEntryOffset = 0x00;   // ULONG
    private const int CreateTime = 0x20;        // LARGE_INTEGER, FILETIME
    private const int UserTime = 0x28;          // LARGE_INTEGER, 100 ns
    private const int KernelTime = 0x30;        // LARGE_INTEGER, 100 ns
    private const int ImageNameLength = 0x38;   // UNICODE_STRING.Length, bytes
    private const int ImageNameBuffer = 0x40;   // UNICODE_STRING.Buffer, absolute pointer
    private const int UniqueProcessId = 0x50;   // HANDLE
    private const int WorkingSetSize = 0x90;    // SIZE_T
    private const int ReadTransferCount = 0xE8; // LARGE_INTEGER, bytes
    private const int WriteTransferCount = 0xF0; // LARGE_INTEGER, bytes

    /// <summary>Size of the fixed part of one entry (the thread array follows it).</summary>
    public const int FixedEntrySize = 0x100;

    /// <summary>
    /// Walks the <c>NextEntryOffset</c> chain and maps each entry to a <see cref="ProcessSample"/>.
    /// <paramref name="baseAddress"/> is the native address <paramref name="buffer"/> was captured
    /// at — the embedded <c>ImageName.Buffer</c> pointers are absolute and resolved against it.
    /// Malformed offsets (out of range, non-advancing) end the walk; garbage values are clamped —
    /// the parser never loops forever or reads out of bounds.
    /// </summary>
    public static IReadOnlyList<ProcessSample> Parse(ReadOnlySpan<byte> buffer, ulong baseAddress)
    {
        var samples = new List<ProcessSample>();
        var offset = 0L;
        while (offset + FixedEntrySize <= buffer.Length)
        {
            var entry = buffer.Slice((int)offset);
            var pid = (int)BinaryPrimitives.ReadUInt64LittleEndian(entry[UniqueProcessId..]);
            var cpuTicks = Math.Max(0, BinaryPrimitives.ReadInt64LittleEndian(entry[KernelTime..]))
                + Math.Max(0, BinaryPrimitives.ReadInt64LittleEndian(entry[UserTime..]));

            samples.Add(new ProcessSample(
                Pid: pid,
                Name: ReadImageName(buffer, entry, baseAddress, pid),
                StartTimeUtc: DateTime.FromFileTimeUtc(Math.Max(0, BinaryPrimitives.ReadInt64LittleEndian(entry[CreateTime..]))),
                TotalCpuTime: new TimeSpan(cpuTicks),
                WorkingSetBytes: (long)BinaryPrimitives.ReadUInt64LittleEndian(entry[WorkingSetSize..]),
                IoReadBytes: Math.Max(0, BinaryPrimitives.ReadInt64LittleEndian(entry[ReadTransferCount..])),
                IoWriteBytes: Math.Max(0, BinaryPrimitives.ReadInt64LittleEndian(entry[WriteTransferCount..]))));

            var next = BinaryPrimitives.ReadUInt32LittleEndian(entry[NextEntryOffset..]);
            if (next == 0)
            {
                break; // last entry
            }

            offset += next;
        }

        return samples;
    }

    private static string ReadImageName(ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> entry, ulong baseAddress, int pid)
    {
        var length = BinaryPrimitives.ReadUInt16LittleEndian(entry[ImageNameLength..]);
        var pointer = BinaryPrimitives.ReadUInt64LittleEndian(entry[ImageNameBuffer..]);
        // The kernel leaves ImageName null for the Idle process (PID 0); Task Manager's name.
        if (length == 0 || pointer == 0)
        {
            return pid == 0 ? "System Idle Process" : string.Empty;
        }

        var nameOffset = pointer - baseAddress; // wraps huge when pointer < baseAddress → rejected below
        if (nameOffset > int.MaxValue || (long)nameOffset + length > buffer.Length)
        {
            return string.Empty; // pointer outside the captured buffer — don't read out of bounds
        }

        return Encoding.Unicode.GetString(buffer.Slice((int)nameOffset, length));
    }
}
