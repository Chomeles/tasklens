using TaskLens.Core.Models;
using TaskLens.Core.Services;

namespace Taskmanager2.App.Services;

// ponytail: deterministic debug stub, same recipe as StubServiceCatalog — the Benutzer page
// renders in DEBUG builds without touching wtsapi32.
internal sealed class StubUserSessionSource : IUserSessionSource
{
    public UserSessionSnapshot Query() => new(
        [
            new UserSession(1, "StubAdmin", "Aktiv"),
            new UserSession(3, "StubGast", "Getrennt"),
        ],
        CatalogAvailability.Available);
}
