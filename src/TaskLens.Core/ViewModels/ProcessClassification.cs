using TaskLens.Core.Models;

namespace TaskLens.Core.ViewModels;

/// <summary>The three real Task-Manager process groups shown as expandable sections on the Prozesse page.</summary>
public enum ProcessGroup
{
    Apps,
    Background,
    System,
}

/// <summary>
/// Classifies a process the way the real Windows 11 Task Manager buckets rows: well-known
/// system process names go to "Windows-Prozesse", processes with a visible top-level window go
/// to "Apps", everything else is "Hintergrundprozesse". Pure/testable — lives in Core.
/// </summary>
public static class ProcessClassification
{
    // ponytail: fixed well-known-name list, not a live registry lookup — matches how real TM's
    // grouping looks in practice for the core OS processes; extend the list if a gap is found.
    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "System Idle Process", "Idle", "Registry", "smss", "csrss", "wininit",
        "winlogon", "services", "lsass", "svchost", "dwm", "fontdrvhost", "sihost", "taskhostw",
        "ntoskrnl", "spoolsv", "lsm", "wlms", "memcompression", "runtimebroker",
    };

    public static ProcessGroup Classify(ProcessSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var name = sample.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? sample.Name[..^4]
            : sample.Name;

        if (SystemProcessNames.Contains(name))
        {
            return ProcessGroup.System;
        }

        return sample.HasVisibleWindow ? ProcessGroup.Apps : ProcessGroup.Background;
    }

    /// <summary>German group header label, real Task-Manager wording.</summary>
    public static string Label(ProcessGroup group) => group switch
    {
        ProcessGroup.Apps => "Apps",
        ProcessGroup.Background => "Hintergrundprozesse",
        ProcessGroup.System => "Windows-Prozesse",
        _ => throw new ArgumentOutOfRangeException(nameof(group), group, "Unknown group."),
    };
}
