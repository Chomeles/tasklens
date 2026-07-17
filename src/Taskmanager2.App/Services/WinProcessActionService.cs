using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

    /// <summary>Real TM's Effizienzmodus: EcoQoS execution-speed throttling + idle priority.</summary>
    public ActionResult SetEfficiencyMode(int pid)
    {
        var handle = OpenProcess(ProcessSetInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return ActionResult.Fail("Zugriff verweigert oder Prozess beendet.");
        }

        try
        {
            var state = new ProcessPowerThrottlingState
            {
                Version = 1,
                ControlMask = PowerThrottlingExecutionSpeed,
                StateMask = PowerThrottlingExecutionSpeed,
            };
            if (!SetProcessInformation(handle, ProcessPowerThrottlingClass, ref state, (uint)Marshal.SizeOf<ProcessPowerThrottlingState>()))
            {
                return ActionResult.Fail(new Win32Exception().Message);
            }

            SetPriorityClass(handle, IdlePriorityClass); // best effort, EcoQoS is the main lever
            return ActionResult.Ok;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>„Neuen Task ausführen": shell launch so URLs/documents work, optional runas.</summary>
    public ActionResult Launch(string command, bool elevated)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return ActionResult.Fail("Kein Befehl angegeben.");
        }

        try
        {
            var info = new ProcessStartInfo { FileName = command.Trim(), UseShellExecute = true };
            if (elevated)
            {
                info.Verb = "runas";
            }

            using var _ = Process.Start(info);
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or System.IO.FileNotFoundException or PlatformNotSupportedException)
        {
            return ActionResult.Fail(ex.Message);
        }
    }

    /// <summary>Details context menu: map the TM priority level to a Win32 priority class.</summary>
    public ActionResult SetPriority(int pid, ProcessPriority priority)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.PriorityClass = priority switch
            {
                ProcessPriority.Idle => ProcessPriorityClass.Idle,
                ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
                ProcessPriority.Normal => ProcessPriorityClass.Normal,
                ProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
                ProcessPriority.High => ProcessPriorityClass.High,
                ProcessPriority.Realtime => ProcessPriorityClass.RealTime,
                _ => ProcessPriorityClass.Normal,
            };
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return ActionResult.Fail(ex.Message);
        }
    }

    /// <summary>Explorer at the exe, entry preselected — real TM's „Dateispeicherort öffnen".</summary>
    public ActionResult OpenFileLocation(int pid)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
        {
            return ActionResult.Fail("Zugriff verweigert oder Prozess beendet.");
        }

        try
        {
            var capacity = 1024;
            var buffer = new char[capacity];
            if (!QueryFullProcessImageName(handle, 0, buffer, ref capacity) || capacity == 0)
            {
                return ActionResult.Fail("Pfad nicht ermittelbar.");
            }

            var path = new string(buffer, 0, capacity);
            using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return ActionResult.Fail(ex.Message);
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>Default browser searching Bing for the process name — real TM's „Onlinesuche".</summary>
    public ActionResult SearchOnline(string processName)
    {
        try
        {
            var query = Uri.EscapeDataString(processName);
            using var _ = Process.Start(new ProcessStartInfo($"https://www.bing.com/search?q={query}") { UseShellExecute = true });
            return ActionResult.Ok;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return ActionResult.Fail(ex.Message);
        }
    }

    private const int ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessSetInformation = 0x0200;
    private const int ProcessPowerThrottlingClass = 4; // PROCESS_INFORMATION_CLASS.ProcessPowerThrottling
    private const uint PowerThrottlingExecutionSpeed = 0x1;
    private const uint IdlePriorityClass = 0x40;

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, char[] exeName, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref ProcessPowerThrottlingState info, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetPriorityClass(IntPtr process, uint priorityClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
