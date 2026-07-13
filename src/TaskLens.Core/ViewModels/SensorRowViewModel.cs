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
    private float? value;

    public SensorRowViewModel(string name, SensorKind kind, float? value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Kind = kind;
        this.value = value;
    }

    public string Name { get; }

    public SensorKind Kind { get; }

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
    public string ValueText => Format(Kind, Value);

    /// <summary>Formats a sensor value with the unit for its kind. Invariant culture, deterministic.</summary>
    public static string Format(SensorKind kind, float? value) =>
        value is not { } v
            ? "—"
            : kind switch
            {
                SensorKind.Temperature => string.Create(CultureInfo.InvariantCulture, $"{v:0.0} °C"),
                SensorKind.Load => string.Create(CultureInfo.InvariantCulture, $"{v:0.0} %"),
                SensorKind.Clock => string.Create(CultureInfo.InvariantCulture, $"{v:0} MHz"),
                SensorKind.Fan => string.Create(CultureInfo.InvariantCulture, $"{v:0} RPM"),
                SensorKind.Power => string.Create(CultureInfo.InvariantCulture, $"{v:0.0} W"),
                SensorKind.Voltage => string.Create(CultureInfo.InvariantCulture, $"{v:0.000} V"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown sensor kind."),
            };
}
