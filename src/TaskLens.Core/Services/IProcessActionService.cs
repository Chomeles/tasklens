namespace TaskLens.Core.Services;

/// <summary>Mutating process actions — Task beenden / Prozessstruktur beenden (plan-tm3 tm3-01).</summary>
public interface IProcessActionService
{
    /// <summary>Terminates the process, optionally with its whole descendant tree.</summary>
    public ActionResult Terminate(int pid, bool entireTree);
}
