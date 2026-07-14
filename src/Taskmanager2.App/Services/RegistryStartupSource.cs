using System.Security;
using Microsoft.Win32;
using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

/// <summary>
/// <see cref="IStartupItemSource"/> over the classic autostart locations: the Run keys of
/// HKLM/HKCU (64-bit view plus the explicit Wow6432Node variant) and both startup folders.
/// Enabled/disabled comes honestly from the <c>Explorer\StartupApproved</c> keys — the same store
/// the real Task Manager reads and writes: first byte of the binary value 0x03 = disabled,
/// anything else (0x02 in practice) or no record at all = enabled. Sources degrade individually
/// (best effort); only a fully unreadable set reports AccessDenied. Since tm3-06 it also
/// implements <see cref="IStartupManager"/>: toggling writes the same 12-byte StartupApproved
/// blob the real Task Manager writes (0x02 + zeros to enable, 0x03 + disable-FILETIME).
/// </summary>
internal sealed class RegistryStartupSource : IStartupItemSource, IStartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyWow = @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    // ToggleId format: "<HKLM|HKCU>\n<StartupApproved subkey>\n<value name>" — \n cannot occur in
    // registry value names or file names, so the fields need no escaping.
    private const char IdSeparator = '\n';

    public StartupSnapshot Query()
    {
        var items = new List<StartupItem>();
        var readableSources = 0;
        var collectors = new Action<List<StartupItem>>[]
        {
            i => CollectRunKey(Registry.LocalMachine, RunKey, "Registry (HKLM)", "Run", i),
            i => CollectRunKey(Registry.LocalMachine, RunKeyWow, "Registry (HKLM, 32-Bit)", "Run32", i),
            i => CollectRunKey(Registry.CurrentUser, RunKey, "Registry (HKCU)", "Run", i),
            i => CollectRunKey(Registry.CurrentUser, RunKeyWow, "Registry (HKCU, 32-Bit)", "Run32", i),
            i => CollectFolder(Environment.SpecialFolder.Startup, Registry.CurrentUser, "Autostart-Ordner (Benutzer)", i),
            i => CollectFolder(Environment.SpecialFolder.CommonStartup, Registry.LocalMachine, "Autostart-Ordner (Alle Benutzer)", i),
        };

        // Best effort per source, like ScmServiceCatalog per service: one locked key or a Run key
        // deleted mid-query (IOException) must not take down the readable ones — and never the
        // tick handler. AccessDenied only when nothing was readable at all.
        foreach (var collect in collectors)
        {
            try
            {
                collect(items);
                readableSources++;
            }
            catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
            {
            }
        }

        return readableSources == 0
            ? new StartupSnapshot([], CatalogAvailability.AccessDenied)
            : new StartupSnapshot(items, CatalogAvailability.Available);
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

            items.Add(new StartupItem(
                name, run.GetValue(name)?.ToString() ?? "", source, IsEnabled(approved, name), MakeId(hive, approvedSubkey, name)));
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
            // A file literally named ".lnk" has an empty stem — fall back to the full file name
            // rather than tripping StartupItem's non-empty guard.
            var stem = Path.GetFileNameWithoutExtension(lnk);
            var name = stem.Length > 0 ? stem : Path.GetFileName(lnk);

            // ponytail: Command is the .lnk path itself — resolving the shortcut target needs
            // COM/IShellLink; add only if someone actually misses the target line.
            items.Add(new StartupItem(
                name, lnk, source, IsEnabled(approved, Path.GetFileName(lnk)), MakeId(hive, "StartupFolder", Path.GetFileName(lnk))));
        }
    }

    /// <summary>
    /// StartupApproved verdict: no record (or no key) = enabled; otherwise the first byte of the
    /// 12-byte binary value decides — 0x03 means disabled, everything else observed (0x02, 0x06)
    /// means enabled.
    /// </summary>
    private static bool IsEnabled(RegistryKey? approved, string name) =>
        approved?.GetValue(name) is not byte[] { Length: > 0 } value || value[0] != 0x03;

    private static string MakeId(RegistryKey hive, string approvedSubkey, string valueName) =>
        (ReferenceEquals(hive, Registry.LocalMachine) ? "HKLM" : "HKCU") + IdSeparator + approvedSubkey + IdSeparator + valueName;

    /// <summary>Writes the StartupApproved blob; access denied (HKLM without admin) comes back as data.</summary>
    public ActionResult SetEnabled(StartupItem item, bool enabled)
    {
        if (item.ToggleId?.Split(IdSeparator) is not [var hiveName, var approvedSubkey, var valueName])
        {
            return ActionResult.Fail("Dieser Eintrag kann nicht umgeschaltet werden.");
        }

        try
        {
            var hive = hiveName == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
            using var approved = hive.CreateSubKey(ApprovedBase + approvedSubkey, writable: true);

            // The real TM's on-disk format: byte 0 is the verdict, bytes 4..11 the disable-time
            // FILETIME (zeros when enabled) — Explorer shows that timestamp in its startup UI.
            var value = new byte[12];
            value[0] = enabled ? (byte)0x02 : (byte)0x03;
            if (!enabled)
            {
                BitConverter.TryWriteBytes(value.AsSpan(4), DateTime.UtcNow.ToFileTimeUtc());
            }

            approved.SetValue(valueName, value, RegistryValueKind.Binary);
            return ActionResult.Ok;
        }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException)
        {
            return ActionResult.Fail(e.Message);
        }
    }
}
