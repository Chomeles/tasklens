namespace TaskLens.Core.Services;

/// <summary>
/// Outcome of a mutating system action (end task, autostart toggle, service control …). Failure
/// (access denied, object gone, protected) is data the view can show, never an exception
/// (plan-tm3 principle).
/// </summary>
public sealed record ActionResult(bool Success, string? Error)
{
    public static ActionResult Ok { get; } = new(true, null);

    public static ActionResult Fail(string error) => new(false, error);
}
