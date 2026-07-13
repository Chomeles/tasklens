namespace TaskLens.Core.Models;

/// <summary>
/// One logon session as observed by the Benutzer page. Read-only by design (plan-tm2.md §2):
/// no disconnect, no logoff. Sessions without a user name (services, listeners) are filtered out
/// by the source, like the real Task Manager does. <paramref name="State"/> is the user-facing,
/// German-mapped connect state („Aktiv", „Getrennt" …).
/// </summary>
public sealed record UserSession(int SessionId, string UserName, string State)
{
    public string UserName { get; init; } =
        !string.IsNullOrWhiteSpace(UserName)
            ? UserName
            : throw new ArgumentException("UserName must be non-empty.", nameof(UserName));

    public string State { get; init; } =
        !string.IsNullOrWhiteSpace(State)
            ? State
            : throw new ArgumentException("State must be non-empty.", nameof(State));
}
