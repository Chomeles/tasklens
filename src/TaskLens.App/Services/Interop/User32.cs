using System.Runtime.InteropServices;

namespace TaskLens.App.Services.Interop;

/// <summary>user32 P/Invoke surface for classifying processes as "Apps" (visible top-level window) —
/// Taskmanager2's Prozesse-page grouping, gap 1.</summary>
internal static partial class User32
{
    internal delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc enumProc, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint hWnd);

    [LibraryImport("user32.dll")]
    internal static partial int GetWindowTextLengthW(nint hWnd);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    /// <summary>
    /// PIDs of processes owning at least one visible top-level window with a non-empty title —
    /// the same heuristic the real Task Manager uses to bucket a process under "Apps" instead of
    /// "Hintergrundprozesse". Walked fresh each call; cheap (a few hundred windows, no per-window
    /// allocation beyond the result set).
    /// </summary>
    internal static HashSet<int> GetPidsWithVisibleWindows()
    {
        var pids = new HashSet<int>();
        EnumWindows((hWnd, _) =>
        {
            if (IsWindowVisible(hWnd) && GetWindowTextLengthW(hWnd) > 0)
            {
                GetWindowThreadProcessId(hWnd, out var pid);
                pids.Add((int)pid);
            }

            return true;
        }, 0);
        return pids;
    }
}
