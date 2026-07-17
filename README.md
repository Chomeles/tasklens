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

The repo also contains **Taskmanager2** (`src/Taskmanager2.App`) — a near-1:1 clone of the
Windows 11 Task Manager (German locale) built on the same TaskLens.Core pipeline. It reproduces the
real layout (collapsed left navigation with Segoe Fluent Icons glyphs, Mica backdrop, search box in
the title bar, German labels: Prozesse, Leistung, App-Verlauf, Autostart-Apps, Benutzer, Details,
Dienste, Einstellungen) and every displayed value is real — nothing is fabricated. Columns without
a backing data source (per-process Netzwerk) stay honestly at the real app's zero-value text
instead of inventing numbers.

**Visual fidelity:** yellow→orange heat tint on every metric column (CPU, Arbeitsspeicher,
Datenträger, Netzwerk) and the tinted two-line aggregate headers, collapsible Apps /
Hintergrundprozesse / Windows-Prozesse groups, expandable app rows with their window title, real
per-process icons (with a shell stock-icon fallback), decimal-comma formatting (`12,4 %`,
`64,0 MB`), and the App-Theme / Standardstartseite / update-speed settings the real app has.

**Functional parity:** real actions throughout — Task beenden / Prozessstruktur beenden,
Effizienzmodus (EcoQoS throttling), Neuen Task ausführen (run dialog on every page), Priorität
festlegen, Dateispeicherort öffnen, Onlinesuche, Autostart aktivieren/deaktivieren, Dienste
Starten/Beenden/Neu starten, Benutzer Verbindung trennen/Abmelden. Leistung shows CPU uptime,
memory composition, per-adapter network graphs, and disk active-time/response via PDH. Details
carries the real Benutzername and Architektur columns from token/`IsWow64Process2` reads. The one
deliberate gap: per-process network attribution stays at 0 MBit/s — real values would need an ETW
trace pipeline (which the real Task Manager itself only approximates).

No Microsoft assets are used — no logos, product icons, or artwork; the name and app icon are its
own. The resemblance is layout homage only (nav labels, column names, Mica). Taskmanager2 ships
from source only (no releases, no packaging).

### Build & visually compare on Windows

Taskmanager2 is WinUI 3 — it only builds and runs on Windows (the Linux solution filter builds
Core + tests only). To check it against the real Task Manager:

```
git checkout tm2-fidelity-prozesse
dotnet build TaskLens.sln -c Release
dotnet run --project src/Taskmanager2.App -c Release
```

Then open the real Windows Task Manager side by side and compare page by page — spacing, fonts,
colour tints, and behaviour. Anything that differs is the next concrete fidelity round.

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
