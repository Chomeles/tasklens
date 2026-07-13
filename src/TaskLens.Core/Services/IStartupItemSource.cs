using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>One query's autostart entries plus why they may be missing.</summary>
public sealed record StartupSnapshot(
    IReadOnlyList<StartupItem> Items, CatalogAvailability Availability)
{
    public IReadOnlyList<StartupItem> Items { get; init; } =
        Items ?? throw new ArgumentNullException(nameof(Items));
}

/// <summary>
/// Reads the configured autostart entries (Run keys, startup folders). Read-only by design
/// (plan-tm2.md §2): no enable/disable, no mutation API of any kind. Access denied is modelled
/// as data, never exceptions.
/// </summary>
public interface IStartupItemSource
{
    public StartupSnapshot Query();
}
