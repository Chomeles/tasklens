using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

// ponytail: deterministic debug stub, same recipe as StubServiceCatalog — the Autostart page
// renders in DEBUG builds without touching the real registry or startup folders.
internal sealed class StubStartupSource : IStartupItemSource
{
    public StartupSnapshot Query() => new(
        [
            new StartupItem(
                "Stub-Cloud-Sync", @"C:\Program Files\Stub\CloudSync.exe /background",
                "Registry (HKLM)", Enabled: true),
            new StartupItem(
                "Stub-Updater", @"C:\Program Files (x86)\Stub\Updater.exe --quiet",
                "Registry (HKLM, 32-Bit)", Enabled: false),
            new StartupItem(
                "Stub-Begrüßung", @"C:\Users\Stub\AppData\Roaming\Stub\Hallo.exe",
                "Registry (HKCU)", Enabled: true),
            new StartupItem(
                "Stub-Notizen",
                @"C:\Users\Stub\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Stub-Notizen.lnk",
                "Autostart-Ordner (Benutzer)", Enabled: true),
        ],
        CatalogAvailability.Available);
}
