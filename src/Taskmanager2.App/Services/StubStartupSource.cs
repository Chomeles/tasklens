using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

// ponytail: deterministic debug stub, same recipe as StubServiceCatalog — the Autostart page
// renders in DEBUG builds without touching the real registry or startup folders. Stateful since
// tm3-06 so the Aktivieren/Deaktivieren flow is exercisable on stub data.
internal sealed class StubStartupSource : IStartupItemSource, IStartupManager
{
    private readonly List<StartupItem> items =
    [
        new StartupItem(
            "Stub-Cloud-Sync", @"C:\Program Files\Stub\CloudSync.exe /background",
            "Registry (HKLM)", Enabled: true, ToggleId: "stub-0"),
        new StartupItem(
            "Stub-Updater", @"C:\Program Files (x86)\Stub\Updater.exe --quiet",
            "Registry (HKLM, 32-Bit)", Enabled: false, ToggleId: "stub-1"),
        new StartupItem(
            "Stub-Begrüßung", @"C:\Users\Stub\AppData\Roaming\Stub\Hallo.exe",
            "Registry (HKCU)", Enabled: true, ToggleId: "stub-2"),
        new StartupItem(
            "Stub-Notizen",
            @"C:\Users\Stub\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Stub-Notizen.lnk",
            "Autostart-Ordner (Benutzer)", Enabled: true, ToggleId: "stub-3"),
    ];

    public StartupSnapshot Query() => new([.. items], CatalogAvailability.Available);

    public ActionResult SetEnabled(StartupItem item, bool enabled)
    {
        var index = items.FindIndex(i => i.ToggleId == item.ToggleId);
        if (index < 0)
        {
            return ActionResult.Fail("Eintrag nicht gefunden.");
        }

        items[index] = items[index] with { Enabled = enabled };
        return ActionResult.Ok;
    }
}
