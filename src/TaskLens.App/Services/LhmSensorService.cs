using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using TaskLens.Core.Services;

namespace TaskLens.App.Services;

/// <summary>
/// <see cref="ISensorService"/> backed by LibreHardwareMonitorLib. <c>Computer</c> is not
/// thread-safe (research.md §2), so one dedicated background thread owns the whole
/// open → update → close lifecycle and publishes immutable snapshots; <see cref="Sample"/>
/// (called from the engine tick) only reads the latest published one. All tree→model mapping
/// is <see cref="LhmMapping"/> in Core, unit-tested on Linux.
/// </summary>
internal sealed class LhmSensorService : ISensorService, IDisposable
{
    // ponytail: fixed 1 s cadence matching the engine default; wire to ISettingsStore in task 14 if needed.
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private readonly ManualResetEventSlim stop = new();
    private readonly ManualResetEventSlim firstSample = new();
    private readonly Thread thread;
    private volatile SensorSnapshot latest;

    public LhmSensorService()
    {
        // Until the thread publishes (Open() can take seconds), report an honest empty state.
        latest = LhmMapping.BuildSnapshot([], Environment.IsPrivilegedProcess, IsPawnIoInstalled());
        thread = new Thread(SampleLoop) { IsBackground = true, Name = "TaskLens.LhmSampler" };
        thread.Start();
    }

    public SensorSnapshot Sample() => latest;

    /// <summary>Blocks until the sampling thread published its first real snapshot. For smoke tests.</summary>
    internal bool WaitForFirstSample(TimeSpan timeout) => firstSample.Wait(timeout);

    public void Dispose()
    {
        stop.Set();
        // Computer.Close() runs on the thread's finally; if LHM wedges, the OS reclaims the handles at exit.
        thread.Join(TimeSpan.FromSeconds(10));
        stop.Dispose();
        firstSample.Dispose();
    }

    private void SampleLoop()
    {
        var isElevated = Environment.IsPrivilegedProcess;
        var isPawnIoInstalled = IsPawnIoInstalled();
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            // RAM is covered by ISystemMetricsService; LHM's memory pseudo-sensors would be noise.
        };

        try
        {
            computer.Open(); // slow: enumerates hardware once, on this thread only
            do
            {
                var rows = new List<LhmSensorRow>();
                foreach (var hardware in computer.Hardware)
                {
                    Collect(hardware, rows);
                }

                latest = LhmMapping.BuildSnapshot(rows, isElevated, isPawnIoInstalled);
                firstSample.Set();
            }
            while (!stop.Wait(UpdateInterval));
        }
        catch (Exception)
        {
            // LHM failed outright (broken driver stack, exotic VM): degrade to an explained empty
            // snapshot — sensor absence is data, never an exception into the engine (plan.md §1).
            latest = LhmMapping.BuildSnapshot([], isElevated, isPawnIoInstalled);
            firstSample.Set();
        }
        finally
        {
            computer.Close(); // releases driver handles; safe when Open() never succeeded
        }
    }

    private static void Collect(IHardware hardware, List<LhmSensorRow> rows)
    {
        try
        {
            hardware.Update();
        }
        catch (Exception)
        {
            // One flaky node (typically SMART on odd disks) must not kill the rest of the tree;
            // its sensors keep their last values this tick.
        }

        foreach (var sub in hardware.SubHardware)
        {
            Collect(sub, rows);
        }

        foreach (var sensor in hardware.Sensors)
        {
            rows.Add(new LhmSensorRow(hardware.Name, sensor.Name, sensor.SensorType.ToString(), sensor.Value));
        }
    }

    /// <summary>
    /// The PawnIO installer writes <c>HKLM\SOFTWARE\PawnIO</c> (research.md §2); key present ≈ installed.
    /// ponytail: coarse check feeding the availability banner only — task 15 owns full first-run detection.
    /// </summary>
    private static bool IsPawnIoInstalled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\PawnIO");
        return key is not null;
    }
}
