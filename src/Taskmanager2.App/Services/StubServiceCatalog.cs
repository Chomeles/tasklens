using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

// ponytail: deterministic debug stub, same recipe as StubServices.cs — the Dienste page renders
// in DEBUG builds without touching the real service control manager.
internal sealed class StubServiceCatalog : IServiceCatalog
{
    public ServiceCatalogSnapshot Query() => new(
        [
            new ServiceEntry("StubAudio", "Stub-Audiodienst", 1204, "Verwaltet Audio für den Stub-Modus.", Running: true),
            new ServiceEntry("StubSpooler", "Stub-Druckwarteschlange", 2048, "Puffert Druckaufträge, die nie gedruckt werden.", Running: true),
            new ServiceEntry("StubUpdate", "Stub-Updatedienst", null, "Sucht nach Updates, findet aber nie welche.", Running: false),
            new ServiceEntry("StubTelemetry", "Stub-Telemetrie", null, "", Running: false),
        ],
        ServiceCatalogAvailability.Available);
}
