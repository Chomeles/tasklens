namespace TaskLens.Core.Models;

/// <summary>Kind of a hardware sensor reading. Extended as later tasks map more LHM sensor types.</summary>
public enum SensorKind
{
    Temperature,
    Load,
    Clock,
    Fan,
    Power,
    Voltage,
}
