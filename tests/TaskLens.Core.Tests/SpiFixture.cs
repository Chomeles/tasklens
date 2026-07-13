using System.Buffers.Binary;
using System.Text;

namespace TaskLens.Core.Tests;

/// <summary>
/// Builds byte-exact 64-bit <c>SYSTEM_PROCESS_INFORMATION</c> buffers for parser tests.
/// Field offsets are written here independently of the parser's own constants (both transcribed
/// from https://ntdoc.m417z.com/ntquerysysteminformation), so the tests cross-check the layout.
/// </summary>
internal static class SpiFixture
{
    /// <summary>Pretend native address of buffer byte 0 — embedded name pointers are absolute.</summary>
    internal const ulong BaseAddress = 0x0000_0200_4000_0000;

    internal const int NextEntryOffsetOffset = 0x00;
    internal const int NameLengthOffset = 0x38;
    internal const int NameBufferOffset = 0x40;
    internal const int FixedEntrySize = 0x100;

    internal sealed record Entry(
        int Pid,
        string? Name,
        long CreateTime = 0,
        long KernelTime = 0,
        long UserTime = 0,
        ulong WorkingSet = 0,
        long IoRead = 0,
        long IoWrite = 0);

    /// <summary>
    /// Lays entries out the way the kernel does: fixed part, then variable data (here the image
    /// name; on a real system also the thread array), then the next entry. Fields the parser must
    /// ignore are filled with noise. The last entry gets <c>NextEntryOffset = 0</c>.
    /// </summary>
    internal static byte[] Build(params Entry[] entries)
    {
        var sizes = entries.Select(e => FixedEntrySize + Pad8(NameBytes(e).Length)).ToArray();
        var buffer = new byte[sizes.Sum()];
        var offset = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var span = buffer.AsSpan(offset);
            BinaryPrimitives.WriteUInt32LittleEndian(span, i == entries.Length - 1 ? 0u : (uint)sizes[i]);
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x04..], 17);                        // NumberOfThreads — noise
            BinaryPrimitives.WriteUInt64LittleEndian(span[0x18..], 0xDEAD_BEEF);               // CycleTime — noise
            BinaryPrimitives.WriteInt64LittleEndian(span[0x20..], entry.CreateTime);
            BinaryPrimitives.WriteInt64LittleEndian(span[0x28..], entry.UserTime);
            BinaryPrimitives.WriteInt64LittleEndian(span[0x30..], entry.KernelTime);
            BinaryPrimitives.WriteUInt64LittleEndian(span[0x50..], (ulong)entry.Pid);
            BinaryPrimitives.WriteUInt32LittleEndian(span[0x60..], 123);                       // HandleCount — noise
            BinaryPrimitives.WriteUInt64LittleEndian(span[0x88..], entry.WorkingSet + 4096);   // PeakWorkingSetSize — noise
            BinaryPrimitives.WriteUInt64LittleEndian(span[0x90..], entry.WorkingSet);
            BinaryPrimitives.WriteInt64LittleEndian(span[0xE8..], entry.IoRead);
            BinaryPrimitives.WriteInt64LittleEndian(span[0xF0..], entry.IoWrite);

            var nameBytes = NameBytes(entry);
            if (nameBytes.Length > 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(span[NameLengthOffset..], (ushort)nameBytes.Length);
                BinaryPrimitives.WriteUInt16LittleEndian(span[(NameLengthOffset + 2)..], (ushort)(nameBytes.Length + 2));
                BinaryPrimitives.WriteUInt64LittleEndian(span[NameBufferOffset..], BaseAddress + (ulong)(offset + FixedEntrySize));
                nameBytes.CopyTo(span[FixedEntrySize..]);
            }

            offset += sizes[i];
        }

        return buffer;
    }

    private static byte[] NameBytes(Entry entry) => entry.Name is null ? [] : Encoding.Unicode.GetBytes(entry.Name);

    private static int Pad8(int length) => (length + 7) & ~7;
}
