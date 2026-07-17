using System.Diagnostics;
using TaskLens.App.Services.Interop;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace TaskLens.App.Services;

/// <summary>
/// <see cref="IProcessEnumerator"/> backed by <c>NtQuerySystemInformation(SystemProcessInformation)</c>:
/// one syscall returns name, PID, CPU times, working set and IO transfer counters for every process,
/// with no per-process handles (research.md §3). Falls back to <see cref="Process.GetProcesses"/>
/// permanently if the NT path ever fails (missing export, unexpected NTSTATUS, layout change).
/// </summary>
internal sealed class NtProcessEnumerator : IProcessEnumerator
{
    private byte[] buffer = new byte[512 * 1024]; // grown on demand, reused across ticks
    private bool ntAvailable = true;

    public IReadOnlyList<ProcessSample> Enumerate()
    {
        if (ntAvailable)
        {
            try
            {
                return EnumerateNt();
            }
            catch (Exception)
            {
                ntAvailable = false; // ponytail: one failure disables the NT path for good — no per-tick retry cost
            }
        }

        return EnumerateFallback();
    }

    /// <summary>The real NT path; throws on failure. Internal so Windows CI smoke tests hit it directly.</summary>
    internal unsafe IReadOnlyList<ProcessSample> EnumerateNt()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            fixed (byte* pinned = buffer)
            {
                var status = NtDll.NtQuerySystemInformation(
                    NtDll.SystemProcessInformation, (nint)pinned, (uint)buffer.Length, out var returnLength);
                if (status == 0)
                {
                    // Parse while pinned: ImageName pointers inside the buffer are absolute addresses.
                    var used = returnLength > 0 && returnLength <= buffer.Length ? (int)returnLength : buffer.Length;
                    var samples = SystemProcessInformationParser.Parse(buffer.AsSpan(0, used), (ulong)pinned);
                    return WithVisibleWindowFlag(samples);
                }

                if (status != NtDll.StatusInfoLengthMismatch)
                {
                    throw new InvalidOperationException($"NtQuerySystemInformation failed: 0x{status:X8}");
                }

                // Required size + slack for processes started between the two calls.
                buffer = new byte[Math.Max(buffer.Length * 2, (int)returnLength + 64 * 1024)];
            }
        }

        throw new InvalidOperationException("NtQuerySystemInformation: buffer kept growing, giving up.");
    }

    /// <summary>
    /// Managed fallback. No IO counters (would need one handle per process), and names lack the
    /// ".exe" suffix the NT path includes — good enough for a degraded mode.
    /// </summary>
    internal static IReadOnlyList<ProcessSample> EnumerateFallback()
    {
        var visibleWindowPids = Interop.User32.GetPidsWithVisibleWindows();
        var processes = Process.GetProcesses();
        var samples = new List<ProcessSample>(processes.Length);
        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    samples.Add(new ProcessSample(
                        Pid: process.Id,
                        Name: process.ProcessName,
                        StartTimeUtc: process.StartTime.ToUniversalTime(),
                        TotalCpuTime: process.TotalProcessorTime,
                        WorkingSetBytes: process.WorkingSet64,
                        IoReadBytes: 0,
                        IoWriteBytes: 0,
                        HasVisibleWindow: visibleWindowPids.Contains(process.Id)));
                }
                catch (Exception)
                {
                    // Access denied or the process exited mid-read — skip it (research.md §3).
                }
            }
        }

        return samples;
    }

    /// <summary>Stamps <see cref="ProcessSample.HasVisibleWindow"/> from a fresh top-level-window
    /// walk (Taskmanager2 Apps/Hintergrundprozesse grouping, gap 1).</summary>
    private static IReadOnlyList<ProcessSample> WithVisibleWindowFlag(IReadOnlyList<ProcessSample> samples)
    {
        var visibleWindowPids = Interop.User32.GetPidsWithVisibleWindows();
        if (visibleWindowPids.Count == 0)
        {
            return samples;
        }

        var result = new List<ProcessSample>(samples.Count);
        foreach (var sample in samples)
        {
            result.Add(visibleWindowPids.Contains(sample.Pid) ? sample with { HasVisibleWindow = true } : sample);
        }

        return result;
    }
}
