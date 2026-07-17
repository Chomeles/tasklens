# TaskLens

A Windows task manager that shows what the built-in one hides: temperatures, power draw, fan speeds, per-process GPU — without needing three extra tools.

Status: in development.

## Features

| Feature | Task Manager | HWiNFO | TaskLens |
|---|---|---|---|
| Process list, sort/filter | Yes | No | Yes |
| Per-process GPU % | Limited | No | Yes |
| CPU temperature / power | No | Yes | Yes |
| Fan speeds | No | Yes | Yes |
| Per-process/sensor history sparklines | No | Limited | Yes |
| Works without admin (processes, GPU, RAM, disk) | Yes | Yes | Yes |
| Single portable exe, no install | No | Yes | Yes |

## Screenshots

_Coming soon — placeholders below will be replaced once the UI stabilizes._

- `docs/screenshots/processes.png` — Processes page
- `docs/screenshots/sensors.png` — Sensors page with sparklines
- `docs/screenshots/details.png` — Per-process/system history

## Download

Prebuilt self-contained zips (win-x64, win-arm64) with SHA-256 checksums are published on the
[Releases](../../releases) page for every `vX.Y.Z` tag. A [winget](packaging/winget) manifest is
maintained for submission to `microsoft/winget-pkgs`.

Admin elevation + [PawnIO](https://pawnio.eu) unlock CPU temperature/power/fan sensors via
LibreHardwareMonitor; without them TaskLens still shows the process list, GPU usage, RAM, and disk.

## Taskmanager2

The repo also contains **Taskmanager2** (`src/Taskmanager2.App`) — a faithful clone of the
Windows 11 Task Manager (German locale) built on the same TaskLens.Core pipeline. It matches the
real layout (collapsed-by-default left navigation with Segoe Fluent Icons glyphs, Mica backdrop,
German labels: Prozesse, Leistung, App-Verlauf, Autostart-Apps, Benutzer, Details, Dienste, plus a
stub Einstellungen item) and shows only the columns the real app shows — no extra sensor columns,
sparklines, or history graphs. Every displayed value is real and comes from the same sampling
pipeline as TaskLens; nothing is fabricated, and columns without a backing data source (e.g.
Status, Netzwerk) stay honestly empty or show the real Task Manager's zero-value text rather than
invented numbers.

The Prozesse page groups rows into Apps / Hintergrundprozesse / Windows-Prozesse (real TM's
grouping) via `TaskLens.Core.ViewModels.ProcessClassification` — well-known system process names
go to Windows-Prozesse, processes with a visible top-level window (detected via a `user32.dll`
window walk in the Windows-only sampler) go to Apps, everything else is Hintergrundprozesse; group
headers show live counts. CPU cells are tinted with a yellow→orange heatmap
(`TaskLens.Core.ViewModels.HeatMap`, a pure value→ARGB function, tested on Linux) on both the
Prozesse and Details pages — Arbeitsspeicher/Datenträger cells are intentionally left untinted
because Core only has bytes/bytes-per-second there, not a 0–100% figure, and inventing a scaling
max would be fake data. Column headers show the real system-wide CPU%/memory% totals from
`SystemSnapshot`. Per-process icons and interactive group-collapse chevrons are not implemented yet
(icon extraction needs Windows GDI/shell APIs and collapse needs a hand-rolled grouped-list
control — both unverifiable without a Windows dev box); a generic/no icon and non-collapsible
group headers are the current, honest state.

Extra hardware sensor detail (temperature, power, fan) is intentionally not shown yet — that lands
as a separate, later step, once it can be added without turning the clone back into the old
sensor-dense satire.

Taskmanager2 is strictly read-only: it never starts or stops services, toggles autostart entries,
or touches user sessions.

No Microsoft assets are used — no logos, product icons, or artwork; the name and app icon are its
own. The resemblance is layout homage only (nav labels, column names, Mica). Taskmanager2 ships
from source only (no releases, no packaging); it builds as part of the normal Windows build:

```
dotnet build TaskLens.sln -c Release
```

Screenshot placeholders, replaced once the UI stabilizes:

- `docs/screenshots/tm2-prozesse.png` — Prozesse page, real Task-Manager column set
- `docs/screenshots/tm2-details.png` — Details page, dense real-data table

## Building

### Windows (full app)

```
dotnet build TaskLens.sln -c Release
```

### Linux (Core + tests only, no Windows dependencies)

```
dotnet build TaskLens.Linux.slnf -c Release
dotnet test tests/TaskLens.Core.Tests -c Release --no-build
dotnet format TaskLens.Linux.slnf --verify-no-changes
```

### Release publish (self-contained, per architecture)

Run on Windows; produces a self-contained folder with no .NET runtime dependency:

```
dotnet publish src/TaskLens.App/TaskLens.App.csproj -c Release -r win-x64  --self-contained true -o publish/win-x64
dotnet publish src/TaskLens.App/TaskLens.App.csproj -c Release -r win-arm64 --self-contained true -o publish/win-arm64
```

`.github/workflows/release.yml` runs these same commands on every push (dry run) and on `v*` tags
(publishes zips + `SHA256SUMS.txt` to a GitHub Release).
