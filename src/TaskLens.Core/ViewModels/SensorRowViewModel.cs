using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>
/// One sensor row inside a hardware group. Identity is <c>(Name, Kind)</c> and never changes;
/// <see cref="Value"/> is updated in place per snapshot so bindings survive across ticks.
/// </summary>
public sealed class SensorRowViewModel : ObservableObject
{
    // ponytail: 60 points ≈ one minute at the default 1 s tick. Row-local history restarts on a
    // structural rebuild (first snapshot only in practice); feed from SamplingEngine.GetSensorHistory
    // if that ever matters.
    private const int HistoryCapacity = 60;

    private readonly HistoryBuffer<float?> history = new(HistoryCapacity);
    private float? value;
    private TemperatureUnit unit;

    public SensorRowViewModel(string name, SensorKind kind, float? value, TemperatureUnit unit = TemperatureUnit.Celsius)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Kind = kind;
        this.value = value;
        this.unit = unit;
        history.Add(value);
    }

    public string Name { get; }

    public SensorKind Kind { get; }

    /// <summary>Display unit for <see cref="SensorKind.Temperature"/> rows; ignored by other kinds.</summary>
    public TemperatureUnit Unit
    {
        get => unit;
        set
        {
            if (SetProperty(ref unit, value) && Kind == SensorKind.Temperature)
            {
                OnPropertyChanged(nameof(ValueText));
            }
        }
    }

    /// <summary>Raw sensor value; <c>null</c> when the sensor reported no reading this tick.</summary>
    public float? Value
    {
        get => value;
        set
        {
            if (SetProperty(ref this.value, value))
            {
                OnPropertyChanged(nameof(ValueText));
            }
        }
    }

    /// <summary>Display text with unit, e.g. "54.0 °C", "45.2 W", "1200 RPM"; "—" when no reading.</summary>
    public string ValueText => Format(Kind, Value, Unit);

    /// <summary>Recent values, oldest first — the sparkline source. Grows one point per tick via <see cref="Update"/>.</summary>
    public IReadOnlyList<float?> History => history;

    /// <summary>
    /// Per-tick update: sets <see cref="Value"/> and appends to <see cref="History"/> — even when
    /// the value is unchanged, so the sparkline still advances along the time axis.
    /// </summary>
    public void Update(float? newValue)
    {
        Value = newValue;
        history.Add(newValue);
        OnPropertyChanged(nameof(History));
    }

    /// <summary>Formats a sensor value with the unit for its kind. Invariant culture, deterministic.</summary>
    public static string Format(SensorKind kind, float? value, TemperatureUnit unit = TemperatureUnit.Celsius) =>
        value is not { } v
            ? "—"
            : kind switch
            {
                SensorKind.Temperature => unit == TemperatureUnit.Fahrenheit
                    ? string.Create(CultureInfo.InvariantCulture, $"{v * 9 / 5 + 32:0.0} °F")
                    : string.Create(CultureInfo.InvariantCulture, $"{v:0.0} °C"),
                SensorKind.Load => string.Create(CultureInfo.InvariantCulture, $"{v:0.0} %"),
                SensorKind.Clock => string.Create(CultureInfo.InvariantCulture, $"{v:0} MHz"),
                SensorKind.Fan => string.Create(CultureInfo.InvariantCulture, $"{v:0} RPM"),
                SensorKind.Power => string.Create(CultureInfo.InvariantCulture, $"{v:0.0} W"),
                SensorKind.Voltage => string.Create(CultureInfo.InvariantCulture, $"{v:0.000} V"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sensor kind."),
            };
}
