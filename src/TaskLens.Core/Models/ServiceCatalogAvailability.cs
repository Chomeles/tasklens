namespace TaskLens.Core.Models;

/// <summary>
/// Why the service catalog is (un)available — same pattern as <see cref="SensorAvailability"/>:
/// absence is modelled as data, not exceptions, so the Dienste page degrades to an InfoBar.
/// An empty but readable catalog is simply <see cref="Available"/> with zero services.
/// </summary>
public enum ServiceCatalogAvailability
{
    /// <summary>The service control manager was enumerated normally.</summary>
    Available,

    /// <summary>The service control manager refused the enumeration (access denied).</summary>
    AccessDenied,
}
