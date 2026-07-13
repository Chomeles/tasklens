using System.Security;
using Microsoft.Win32;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

/// <summary>
/// Read-only <see cref="IStartupItemSource"/> over the classic autostart locations: the Run keys
/// of HKLM/HKCU (64-bit view plus the explicit Wow6432Node variant) and both startup folders.
/// Enabled/disabled comes honestly from the <c>Explorer\StartupApproved</c> keys — the same store
/// the real Task Manager writes: first byte of the binary value 0x03 = disabled, anything else
/// (0x02 in practice) or no record at all = enabled. No enable/disable API anywhere
/// (plan-tm2.md §2).
/// </summary>
internal sealed class RegistryStartupSource : IStartupItemSource
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyWow = @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    public StartupSnapshot Query()
    {
        try
        {
            var items = new List<StartupItem>();
            CollectRunKey(Registry.LocalMachine, RunKey, "Registry (HKLM)", "Run", items);
            CollectRunKey(Registry.LocalMachine, RunKeyWow, "Registry (HKLM, 32-Bit)", "Run32", items);
            CollectRunKey(Registry.CurrentUser, RunKey, "Registry (HKCU)", "Run", items);
            CollectRunKey(Registry.CurrentUser, RunKeyWow, "Registry (HKCU, 32-Bit)", "Run32", items);
            CollectFolder(Environment.SpecialFolder.Startup, Registry.CurrentUser, "Autostart-Ordner (Benutzer)", items);
            CollectFolder(Environment.SpecialFolder.CommonStartup, Registry.LocalMachine, "Autostart-Ordner (Alle Benutzer)", items);
            return new StartupSnapshot(items, CatalogAvailability.Available);
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException)
        {
            return new StartupSnapshot([], CatalogAvailability.AccessDenied);
        }
    }

    private static void CollectRunKey(
        RegistryKey hive, string runPath, string source, string approvedSubkey, List<StartupItem> items)
    {
        using var run = hive.OpenSubKey(runPath);
        if (run is null)
        {
            return; // key absent (e.g. no Wow6432Node on arm64 without x86 apps) — nothing to list
        }

        using var approved = hive.OpenSubKey(ApprovedBase + approvedSubkey);
        foreach (var name in run.GetValueNames())
        {
            if (name.Length == 0)
            {
                continue; // the key's default value is not a startup entry
            }

            items.Add(new StartupItem(name, run.GetValue(name)?.ToString() ?? "", source, IsEnabled(approved, name)));
        }
    }

    private static void CollectFolder(
        Environment.SpecialFolder folder, RegistryKey hive, string source, List<StartupItem> items)
    {
        var path = Environment.GetFolderPath(folder);
        if (path.Length == 0 || !Directory.Exists(path))
        {
            return;
        }

        // StartupApproved\StartupFolder lives in the hive matching the folder: HKCU for the user
        // folder, HKLM for the all-users folder; value names are the file names incl. ".lnk".
        using var approved = hive.OpenSubKey(ApprovedBase + "StartupFolder");
        foreach (var lnk in Directory.EnumerateFiles(path, "*.lnk"))
        {
            // ponytail: Command is the .lnk path itself — resolving the shortcut target needs
            // COM/IShellLink; add only if someone actually misses the target line.
            items.Add(new StartupItem(
                Path.GetFileNameWithoutExtension(lnk), lnk, source, IsEnabled(approved, Path.GetFileName(lnk))));
        }
    }

    /// <summary>
    /// StartupApproved verdict: no record (or no key) = enabled; otherwise the first byte of the
    /// 12-byte binary value decides — 0x03 means disabled, everything else observed (0x02, 0x06)
    /// means enabled.
    /// </summary>
    private static bool IsEnabled(RegistryKey? approved, string name) =>
        approved?.GetValue(name) is not byte[] { Length: > 0 } value || value[0] != 0x03;
}
