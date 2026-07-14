using System.ComponentModel;
using System.Diagnostics;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

/// <summary>
/// End-task on <see cref="Process.Kill(bool)"/>. Denied access, protected processes and races with
/// process exit come back as <see cref="ActionResult"/> data, never as exceptions (plan-tm3).
/// </summary>
public sealed class WinProcessActionService : IProcessActionService
{
    public ActionResult Terminate(int pid, bool entireTree)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireTree);
            return ActionResult.Ok;
        }
        catch (ArgumentException)
        {
            // Already gone between tick and click — ending it succeeded by definition.
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException or AggregateException)
        {
            return ActionResult.Fail(ex.Message);
        }
    }
}
