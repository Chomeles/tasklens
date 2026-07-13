namespace TaskLens.Core.Models;

/// <summary>Display unit for temperature sensors.</summary>
public enum TemperatureUnit
{
    Celsius,
    Fahrenheit,
}

/// <summary>How per-process CPU% is normalized.</summary>
public enum CpuPercentNormalization
{
    /// <summary>100% = all logical cores busy (Task Manager style).</summary>
    AllCores,

    /// <summary>100% = one logical core busy; multi-threaded processes can exceed 100%.</summary>
    SingleCore,
}

/// <summary>User settings. Persisted by <c>ISettingsStore</c> (task 14); live-applied.</summary>
public sealed record Settings
{
    public static Settings Default { get; } = new();

    private readonly TimeSpan refreshInterval = TimeSpan.FromSeconds(1);

    public TimeSpan RefreshInterval
    {
        get => refreshInterval;
        init => refreshInterval = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "RefreshInterval must be positive.");
    }

    public TemperatureUnit TemperatureUnit { get; init; } = TemperatureUnit.Celsius;

    public CpuPercentNormalization CpuNormalization { get; init; } = CpuPercentNormalization.AllCores;
}
