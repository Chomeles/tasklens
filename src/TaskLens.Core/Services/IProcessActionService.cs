namespace TaskLens.Core.Services;

/// <summary>Mutating process actions — Task beenden / Prozessstruktur beenden (plan-tm3 tm3-01).</summary>
public interface IProcessActionService
{
    /// <summary>Terminates the process, optionally with its whole descendant tree.</summary>
    public ActionResult Terminate(int pid, bool entireTree);

    /// <summary>Efficiency mode: EcoQoS power throttling + idle priority, like the real TM (tm3-02).</summary>
    public ActionResult SetEfficiencyMode(int pid);

    /// <summary>„Neuen Task ausführen": shell-launches the command, optionally elevated (tm3-02).</summary>
    public ActionResult Launch(string command, bool elevated);
}
