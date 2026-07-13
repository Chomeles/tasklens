namespace TaskLens.Core.Models;

/// <summary>
/// One Windows service as observed by the Dienste page. Read-only by design (plan-tm2.md §2):
/// Taskmanager2 observes services, it never starts or stops them.
/// <paramref name="Pid"/> is <c>null</c> for stopped services or when the per-service query is
/// denied; <paramref name="Description"/> is empty when none is configured or the query is denied.
/// </summary>
public sealed record ServiceEntry(string Name, string DisplayName, int? Pid, string Description, bool Running)
{
    public string Name { get; init; } =
        !string.IsNullOrWhiteSpace(Name)
            ? Name
            : throw new ArgumentException("Name must be non-empty.", nameof(Name));

    public string DisplayName { get; init; } =
        DisplayName ?? throw new ArgumentNullException(nameof(DisplayName));

    public string Description { get; init; } =
        Description ?? throw new ArgumentNullException(nameof(Description));
}
