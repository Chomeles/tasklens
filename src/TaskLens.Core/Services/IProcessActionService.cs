namespace TaskLens.Core.Services;

/// <summary>
/// Outcome of a mutating process action. Failure (access denied, process protected) is data the
/// view can show, never an exception (plan-tm3 §0 principle).
/// </summary>
public sealed record ProcessActionResult(bool Success, string? Error)
{
    public static ProcessActionResult Ok { get; } = new(true, null);

    public static ProcessActionResult Fail(string error) => new(false, error);
}

/// <summary>Mutating process actions — Task beenden / Prozessstruktur beenden (plan-tm3 tm3-01).</summary>
public interface IProcessActionService
{
    /// <summary>Terminates the process, optionally with its whole descendant tree.</summary>
    public ProcessActionResult Terminate(int pid, bool entireTree);
}
