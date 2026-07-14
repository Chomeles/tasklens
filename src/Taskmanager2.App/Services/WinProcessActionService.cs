using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TaskLens.App.Services.Interop;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

/// <summary>
/// Process actions on <see cref="Process"/> + kernel32 (plan-tm3 tm3-01/tm3-02). Denied access,
/// protected processes and races with process exit come back as <see cref="ActionResult"/> data,
/// never as exceptions.
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

    /// <summary>
    /// ShellExecute semantics like the real TM's run box: bare names resolve via PATH/App Paths,
    /// documents open in their default app. The child inherits this process's elevation —
    /// Taskmanager2 runs elevated, so every new task starts with administrator rights.
    /// </summary>
    public ActionResult Run(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = command, UseShellExecute = true });
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return ActionResult.Fail(ex.Message);
        }
    }

    public ActionResult SetPriority(int pid, ProcessPriority priority)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.PriorityClass = priority switch
            {
                ProcessPriority.Realtime => ProcessPriorityClass.RealTime,
                ProcessPriority.High => ProcessPriorityClass.High,
                ProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                ProcessPriority.Normal => ProcessPriorityClass.Normal,
                ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                ProcessPriority.Idle => ProcessPriorityClass.Idle,
                _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown priority."),
            };
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return ActionResult.Fail(ex.Message);
        }
    }

    /// <summary>EcoQoS power throttling + idle priority — the same pair the real TM sets.</summary>
    public ActionResult SetEfficiencyMode(int pid, bool enabled)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            var state = new Kernel32.ProcessPowerThrottlingState
            {
                Version = Kernel32.ProcessPowerThrottlingState.CurrentVersion,
                ControlMask = Kernel32.ProcessPowerThrottlingState.ExecutionSpeed,
                StateMask = enabled ? Kernel32.ProcessPowerThrottlingState.ExecutionSpeed : 0,
            };
            if (!Kernel32.SetProcessInformation(
                process.SafeHandle,
                Kernel32.ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<Kernel32.ProcessPowerThrottlingState>()))
            {
                return ActionResult.Fail(new Win32Exception(Marshal.GetLastPInvokeError()).Message);
            }

            process.PriorityClass = enabled ? ProcessPriorityClass.Idle : ProcessPriorityClass.Normal;
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return ActionResult.Fail(ex.Message);
        }
    }

    public ActionResult OpenFileLocation(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            // MainModule needs PROCESS_QUERY_INFORMATION|VM_READ — fine, Taskmanager2 runs elevated;
            // protected/system processes still refuse and surface as data below.
            var path = process.MainModule?.FileName;
            if (string.IsNullOrEmpty(path))
            {
                return ActionResult.Fail("Der Dateipfad des Prozesses ist nicht verfügbar.");
            }

            using var explorer = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
            });
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException or FileNotFoundException)
        {
            return ActionResult.Fail(ex.Message);
        }
    }
}
