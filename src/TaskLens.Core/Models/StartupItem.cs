namespace TaskLens.Core.Models;

/// <summary>
/// One autostart entry as observed by the Autostart-Apps page.
/// <paramref name="Source"/> is the user-facing origin label („Registry (HKLM)",
/// „Autostart-Ordner (Benutzer)" …); <paramref name="Enabled"/> reflects the StartupApproved
/// registry state — entries without a StartupApproved record are enabled.
/// <paramref name="ToggleId"/> is an opaque handle the producing source's
/// <c>IStartupManager</c> understands (plan-tm3 tm3-06); null marks the entry as not toggleable.
/// </summary>
public sealed record StartupItem(string Name, string Command, string Source, bool Enabled, string? ToggleId = null)
{
    public string Name { get; init; } =
        !string.IsNullOrWhiteSpace(Name)
            ? Name
            : throw new ArgumentException("Name must be non-empty.", nameof(Name));

    public string Command { get; init; } =
        Command ?? throw new ArgumentNullException(nameof(Command));

    public string Source { get; init; } =
        !string.IsNullOrWhiteSpace(Source)
            ? Source
            : throw new ArgumentException("Source must be non-empty.", nameof(Source));
}
