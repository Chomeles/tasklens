namespace TaskLens.Core.Services;

/// <summary>Whether per-process network attribution is delivering real data.</summary>
public enum NetworkAttributionAvailability
{
    /// <summary>The ETW session is running; rates are real.</summary>
    Ok,

    /// <summary>Starting the trace session was denied — needs elevation; all rates stay 0.</summary>
    RequiresAdmin,

    /// <summary>The trace could not be started for another reason; all rates stay 0.</summary>
    Unavailable,
}

/// <summary>Per-process network throughput (send + receive). PIDs absent from the map mean 0 B/s.</summary>
public interface IProcessNetworkService
{
    public NetworkAttributionAvailability Availability { get; }

    public IReadOnlyDictionary<int, double> SampleNetworkBytesPerSecondByPid();
}
