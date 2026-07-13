using System.Buffers.Binary;
using TaskLens.Core.Services;

namespace TaskLens.Core.Tests;

public class SystemProcessInformationParserTests
{
    [Fact]
    public void SingleEntry_MapsEveryField()
    {
        var buffer = SpiFixture.Build(new SpiFixture.Entry(
            Pid: 4242,
            Name: "TaskLens.App.exe",
            CreateTime: 133_600_864_000_000_000,
            KernelTime: 600_000_000,
            UserTime: 1_800_000_000,
            WorkingSet: 150_994_944,
            IoRead: 10_485_760,
            IoWrite: 2_097_152));

        var sample = Assert.Single(SystemProcessInformationParser.Parse(buffer, SpiFixture.BaseAddress));

        Assert.Equal(4242, sample.Pid);
        Assert.Equal("TaskLens.App.exe", sample.Name);
        Assert.Equal(DateTime.FromFileTimeUtc(133_600_864_000_000_000), sample.StartTimeUtc);
        Assert.Equal(new TimeSpan(2_400_000_000), sample.TotalCpuTime); // kernel + user
        Assert.Equal(150_994_944, sample.WorkingSetBytes);
        Assert.Equal(10_485_760, sample.IoReadBytes);
        Assert.Equal(2_097_152, sample.IoWriteBytes);
    }

    [Fact]
    public void EntryChain_FollowsNextEntryOffset_InOrder()
    {
        var buffer = SpiFixture.Build(
            new SpiFixture.Entry(0, null),
            new SpiFixture.Entry(4, "System"),
            new SpiFixture.Entry(1044, "svchost.exe"));

        var samples = SystemProcessInformationParser.Parse(buffer, SpiFixture.BaseAddress);

        Assert.Equal([0, 4, 1044], samples.Select(s => s.Pid));
        Assert.Equal(["System Idle Process", "System", "svchost.exe"], samples.Select(s => s.Name));
    }

    [Fact]
    public void IdleProcess_NullNameAndZeroCreateTime_AreMapped()
    {
        var buffer = SpiFixture.Build(new SpiFixture.Entry(0, null, KernelTime: 8_640_000_000_000));

        var sample = Assert.Single(SystemProcessInformationParser.Parse(buffer, SpiFixture.BaseAddress));

        Assert.Equal("System Idle Process", sample.Name);
        Assert.Equal(DateTime.FromFileTimeUtc(0), sample.StartTimeUtc); // FILETIME epoch, 1601-01-01
        Assert.Equal(TimeSpan.FromHours(240), sample.TotalCpuTime);
    }

    [Fact]
    public void EmptyName_OnNonIdlePid_StaysEmpty()
    {
        var buffer = SpiFixture.Build(new SpiFixture.Entry(77, null));

        Assert.Equal(string.Empty, Assert.Single(SystemProcessInformationParser.Parse(buffer, SpiFixture.BaseAddress)).Name);
    }

    [Theory]
    [InlineData(ulong.MaxValue)]                    // garbage
    [InlineData(SpiFixture.BaseAddress - 0x1000)]   // before the buffer (offset wraps negative)
    [InlineData(SpiFixture.BaseAddress + 0x10000)]  // past the end
    public void NamePointerOutsideBuffer_YieldsEmptyName_NoCrash(ulong pointer)
    {
        var buffer = SpiFixture.Build(new SpiFixture.Entry(5, "legit.exe"));
        BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(SpiFixture.NameBufferOffset), pointer);

        Assert.Equal(string.Empty, Assert.Single(SystemProcessInformationParser.Parse(buffer, SpiFixture.BaseAddress)).Name);
    }

    [Fact]
    public void NameRunningPastBufferEnd_YieldsEmptyName()
    {
        var buffer = SpiFixture.Build(new SpiFixture.Entry(5, "legit.exe"));
        // Point the 18-byte name at the last 2 bytes: an unchecked read would run off the end.
        BinaryPrimitives.WriteUInt64LittleEndian(
            buffer.AsSpan(SpiFixture.NameBufferOffset), SpiFixture.BaseAddress + (ulong)(buffer.Length - 2));

        Assert.Equal(string.Empty, Assert.Single(SystemProcessInformationParser.Parse(buffer, SpiFixture.BaseAddress)).Name);
    }

    [Fact]
    public void CorruptNextEntryOffset_StopsAfterCurrentEntry_NoOverflow()
    {
        var buffer = SpiFixture.Build(new SpiFixture.Entry(1, "a.exe"), new SpiFixture.Entry(2, "b.exe"));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(SpiFixture.NextEntryOffsetOffset), uint.MaxValue);

        Assert.Equal(1, Assert.Single(SystemProcessInformationParser.Parse(buffer, SpiFixture.BaseAddress)).Pid);
    }

    [Fact]
    public void BufferSmallerThanOneEntry_ReturnsEmpty()
    {
        Assert.Empty(SystemProcessInformationParser.Parse(
            new byte[SpiFixture.FixedEntrySize - 1], SpiFixture.BaseAddress));
        Assert.Empty(SystemProcessInformationParser.Parse(ReadOnlySpan<byte>.Empty, SpiFixture.BaseAddress));
    }

    [Fact]
    public void NegativeTimesAndCounters_ClampToZero()
    {
        var buffer = SpiFixture.Build(new SpiFixture.Entry(
            9, "x.exe", CreateTime: -5, KernelTime: -1, UserTime: 70, IoRead: -3, IoWrite: -4));

        var sample = Assert.Single(SystemProcessInformationParser.Parse(buffer, SpiFixture.BaseAddress));

        Assert.Equal(DateTime.FromFileTimeUtc(0), sample.StartTimeUtc);
        Assert.Equal(new TimeSpan(70), sample.TotalCpuTime);
        Assert.Equal(0, sample.IoReadBytes);
        Assert.Equal(0, sample.IoWriteBytes);
    }

    [Fact]
    public void CapturedFixtureFile_ParsesExpectedProcessTable()
    {
        // Five-process SYSTEM_PROCESS_INFORMATION buffer, 64-bit layout, captured at base address
        // SpiFixture.BaseAddress (synthesized with SpiFixture.Build — a capture from a real
        // Windows box can replace it; update the literals below to match).
        var buffer = File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "system_process_information_x64.bin"));

        var samples = SystemProcessInformationParser.Parse(buffer, SpiFixture.BaseAddress);

        Assert.Equal([0, 4, 1044, 4242, 888], samples.Select(s => s.Pid));
        Assert.Equal(
            ["System Idle Process", "System", "svchost.exe", "TaskLens.App.exe", "chrome.exe"],
            samples.Select(s => s.Name));

        var system = samples[1];
        Assert.Equal(DateTime.FromFileTimeUtc(133_600_000_000_000_000), system.StartTimeUtc);
        Assert.Equal(TimeSpan.FromHours(1), system.TotalCpuTime);
        Assert.Equal(1_234_944, system.WorkingSetBytes);
        Assert.Equal(5_000_000_000, system.IoReadBytes);
        Assert.Equal(1_000_000_000, system.IoWriteBytes);

        var app = samples[3];
        Assert.Equal(DateTime.FromFileTimeUtc(133_600_864_000_000_000), app.StartTimeUtc);
        Assert.Equal(new TimeSpan(2_400_000_000), app.TotalCpuTime);
        Assert.Equal(150_994_944, app.WorkingSetBytes);
        Assert.Equal(10_485_760, app.IoReadBytes);
        Assert.Equal(2_097_152, app.IoWriteBytes);
    }
}
