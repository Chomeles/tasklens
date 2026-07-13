using System.Runtime.InteropServices;
using TaskLens.App.Services.Interop;
using TaskLens.Core.Services;

namespace TaskLens.App.Services;

/// <summary>
/// Real Windows system totals: RAM via <c>GlobalMemoryStatusEx</c>, total CPU as the busy share of
/// the <c>GetSystemTimes</c> delta since the previous sample (kernel time includes idle time).
/// The first, unprimed sample reports 0 % CPU; API failure degrades to zeros, never an exception.
/// </summary>
internal sealed class WinSystemMetricsService : ISystemMetricsService
{
    private ulong lastIdle;
    private ulong lastKernel;
    private ulong lastUser;
    private bool primed;

    public SystemMetrics Sample()
    {
        var memory = new Kernel32.MemoryStatusEx { Length = (uint)Marshal.SizeOf<Kernel32.MemoryStatusEx>() };
        long total = 0;
        long used = 0;
        if (Kernel32.GlobalMemoryStatusEx(ref memory))
        {
            total = (long)memory.TotalPhys;
            used = total - (long)Math.Min(memory.AvailPhys, memory.TotalPhys);
        }

        var cpu = 0.0;
        if (Kernel32.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            if (primed)
            {
                var totalDelta = (kernel - lastKernel) + (user - lastUser);
                var idleDelta = idle - lastIdle;
                if (totalDelta > 0)
                {
                    cpu = Math.Clamp(100.0 * (totalDelta - idleDelta) / totalDelta, 0.0, 100.0);
                }
            }

            (lastIdle, lastKernel, lastUser, primed) = (idle, kernel, user, true);
        }

        return new SystemMetrics(cpu, used, total);
    }
}
