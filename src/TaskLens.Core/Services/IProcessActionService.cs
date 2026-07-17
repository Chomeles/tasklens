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

    /// <summary>Sets the process priority class (Details context menu, tm3-05-extended).</summary>
    public ActionResult SetPriority(int pid, ProcessPriority priority);
}

/// <summary>The real TM's six priority levels, ordered low→high.</summary>
public enum ProcessPriority
{
    Idle,
    BelowNormal,
    Normal,
    AboveNormal,
    High,
    Realtime,
}
