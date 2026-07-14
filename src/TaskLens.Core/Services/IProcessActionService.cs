using TaskLens.Core.Models;

namespace TaskLens.Core.Services;

/// <summary>
/// Mutating process actions (plan-tm3 tm3-01/tm3-02): end task, run new task, priority,
/// efficiency mode, open file location. Every failure mode is <see cref="ActionResult"/> data.
/// </summary>
public interface IProcessActionService
{
    /// <summary>Terminates the process, optionally with its whole descendant tree.</summary>
    public ActionResult Terminate(int pid, bool entireTree);

    /// <summary>Starts a new task from a command line („Neuen Task ausführen").</summary>
    public ActionResult Run(string command);

    /// <summary>Sets the process priority class.</summary>
    public ActionResult SetPriority(int pid, ProcessPriority priority);

    /// <summary>Enables/disables efficiency mode (EcoQoS power throttling + idle priority).</summary>
    public ActionResult SetEfficiencyMode(int pid, bool enabled);

    /// <summary>Reveals the process image in its folder („Dateipfad öffnen").</summary>
    public ActionResult OpenFileLocation(int pid);
}
