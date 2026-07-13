namespace TaskLens.Core.Services;

/// <summary>Per-process GPU utilization. PIDs absent from the map mean 0%.</summary>
public interface IGpuProcessService
{
    public IReadOnlyDictionary<int, double> SampleGpuPercentByPid();
}
