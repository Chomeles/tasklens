using TaskLens.Core.Services;

namespace TaskLens.Core.Tests;

public class GpuEngineAggregatorTests
{
    [Theory]
    [InlineData("pid_1234_luid_0x00000000_0x0000ABCD_phys_0_eng_0_engtype_3D", true, 1234)]
    [InlineData("pid_0_luid_0x0_0x0_phys_0_eng_0_engtype_Copy", true, 0)]
    [InlineData("pid_42", true, 42)]
    [InlineData("luid_0x0_0x0_phys_0_eng_0_engtype_3D", false, 0)]
    [InlineData("pid_", false, 0)]
    [InlineData("pid_abc_eng_0", false, 0)]
    [InlineData("", false, 0)]
    public void TryParsePid_ParsesOrRejects(string instanceName, bool expectedOk, int expectedPid)
    {
        var ok = GpuEngineAggregator.TryParsePid(instanceName, out var pid);

        Assert.Equal(expectedOk, ok);
        if (expectedOk)
        {
            Assert.Equal(expectedPid, pid);
        }
    }

    [Fact]
    public void AggregateMaxByPid_TakesMaxAcrossEngines()
    {
        var counters = new[]
        {
            ("pid_1234_eng_0_engtype_3D", 12.5),
            ("pid_1234_eng_1_engtype_Copy", 40.0),
            ("pid_1234_eng_2_engtype_VideoDecode", 3.0),
            ("pid_9_eng_0_engtype_3D", 99.9),
        };

        var result = GpuEngineAggregator.AggregateMaxByPid(counters);

        Assert.Equal(2, result.Count);
        Assert.Equal(40.0, result[1234]);
        Assert.Equal(99.9, result[9]);
    }

    [Fact]
    public void AggregateMaxByPid_IgnoresUnparsableInstances()
    {
        var counters = new[]
        {
            ("_Total", 55.0),
            ("pid_5_eng_0_engtype_3D", 10.0),
        };

        var result = GpuEngineAggregator.AggregateMaxByPid(counters);

        Assert.Single(result);
        Assert.Equal(10.0, result[5]);
    }

    [Fact]
    public void AggregateMaxByPid_EmptyInput_EmptyMap()
    {
        var result = GpuEngineAggregator.AggregateMaxByPid([]);

        Assert.Empty(result);
    }
}
