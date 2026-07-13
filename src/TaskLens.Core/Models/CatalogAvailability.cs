namespace TaskLens.Core.Models;

/// <summary>
/// Why a read-only data source is (un)available — same pattern as
/// <see cref="ServiceCatalogAvailability"/>: absence is modelled as data, not exceptions, so the
/// page degrades to an InfoBar. Shared by the Autostart and Benutzer sources; an empty but
/// readable source is simply <see cref="Available"/> with zero items.
/// </summary>
/// <remarks>
/// ponytail: <see cref="ServiceCatalogAvailability"/> predates this enum and stays as-is —
/// folding it in would be pure rename churn across tm2-06 code and tests.
/// </remarks>
public enum CatalogAvailability
{
    /// <summary>The source was read normally.</summary>
    Available,

    /// <summary>The source refused the query (access denied).</summary>
    AccessDenied,
}
