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

    /// <summary>„Dateispeicherort öffnen": opens Explorer at the process's executable (real TM menu).</summary>
    public ActionResult OpenFileLocation(int pid);

    /// <summary>„Onlinesuche": opens the default browser searching for the process name (real TM menu).</summary>
    public ActionResult SearchOnline(string processName);
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
