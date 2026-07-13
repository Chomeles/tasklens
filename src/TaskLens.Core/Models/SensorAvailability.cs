namespace TaskLens.Core.Models;

/// <summary>
/// Why sensor data is (un)available. Absence is modelled as data, not exceptions,
/// so ViewModels can degrade gracefully (banner states in task 05).
/// </summary>
public enum SensorAvailability
{
    /// <summary>Sensors are being read normally.</summary>
    Available,

    /// <summary>Process is not elevated; hardware access requires administrator rights.</summary>
    NoAdmin,

    /// <summary>Elevated, but the PawnIO driver is not installed.</summary>
    NoPawnIo,

    /// <summary>Hardware access works but no sensors were found (typical in VMs).</summary>
    NoSensors,
}
