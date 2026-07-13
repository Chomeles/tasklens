using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>One query's logon sessions plus why they may be missing.</summary>
public sealed record UserSessionSnapshot(
    IReadOnlyList<UserSession> Sessions, CatalogAvailability Availability)
{
    public IReadOnlyList<UserSession> Sessions { get; init; } =
        Sessions ?? throw new ArgumentNullException(nameof(Sessions));
}

/// <summary>
/// Reads the interactive logon sessions. Read-only by design (plan-tm2.md §2): no disconnect,
/// no logoff, no mutation API of any kind. Access denied is modelled as data, never exceptions.
/// </summary>
public interface IUserSessionSource
{
    public UserSessionSnapshot Query();
}
