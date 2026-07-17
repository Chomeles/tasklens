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

/// <summary>App theme like the real Win11 Task Manager's „App-Theme" setting.</summary>
public enum AppTheme
{
    /// <summary>Follow the Windows system theme.</summary>
    System,
    Light,
    Dark,
}

/// <summary>Which page opens on launch — the real TM's „Standardstartseite" setting.</summary>
public enum StartPage
{
    Prozesse,
    Leistung,
    AppVerlauf,
    Autostart,
    Benutzer,
    Details,
    Dienste,
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

    /// <summary>App-Theme like the real TM: System (default), Hell or Dunkel.</summary>
    public AppTheme Theme { get; init; } = AppTheme.System;

    /// <summary>Standardstartseite like the real TM: the page shown on launch (default Prozesse).</summary>
    public StartPage StartPage { get; init; } = StartPage.Prozesse;
}
