using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>One catalog query's services plus why they may be missing.</summary>
public sealed record ServiceCatalogSnapshot(
    IReadOnlyList<ServiceEntry> Services, ServiceCatalogAvailability Availability)
{
    public IReadOnlyList<ServiceEntry> Services { get; init; } =
        Services ?? throw new ArgumentNullException(nameof(Services));
}

/// <summary>
/// Reads the installed Windows services. Read-only by design (plan-tm2.md §2): no start/stop, no
/// mutation API of any kind. Access denied is modelled as data, never exceptions.
/// </summary>
public interface IServiceCatalog
{
    public ServiceCatalogSnapshot Query();
}
