# Taskmanager2

The task manager you already know — plus everything you'd otherwise install extra tools for.

Taskmanager2 (`src/Taskmanager2.App`) is a near-1:1 recreation of the Windows 11 Task Manager
(German locale: Prozesse, Leistung, App-Verlauf, Autostart-Apps, Benutzer, Details, Dienste) that
folds in the features people install Process Explorer, TCPView, HWiNFO, and Autoruns for. New
capabilities appear only in native Task Manager mechanics — optional columns, context menus,
dialogs — never as bolted-on panels. Every displayed value is real; where a data source or
permission is missing, it honestly shows 0 or empty, exactly like the real app does.

Status: in development, ships from source only (no releases, no packaging).

## What it replaces

| You install… | To see… | In Taskmanager2 |
|---|---|---|
| Process Explorer | command lines, tree kills, throttling | Befehlszeile column in Details, Prozessstruktur beenden, Effizienzmodus (EcoQoS) |
| TCPView | who a process talks to | Netzwerkverbindungen dialog per process — TCP/UDP, v4+v6, no admin needed |
| HWiNFO / LibreHardwareMonitor | temperatures, power, fans | Leistung page: CPU temperature, package power, fan speeds, per-sensor history |
| Autoruns | autostart beyond the registry | Autostart-Apps also reads the user + common Startup folders, resolves `.lnk` targets and publishers, enable/disable works |
| — | real per-process network | Netzwerk column fed by an ETW kernel-network session instead of a hardcoded 0 |

## Feature table

| Feature | Win11 Task Manager | Extra tools | Taskmanager2 |
|---|---|---|---|
| Process list, groups, heat-tinted columns | Yes | — | Yes |
| Command line per process (Details) | No | Process Explorer | Yes |
| Efficiency mode, process tree kill, priority | Yes | Process Explorer | Yes |
| Per-process network throughput | Approximated | — | Yes (ETW, needs admin) |
| Per-process TCP/UDP connections | No | TCPView | Yes (no admin) |
| CPU temperature / power / fan speeds | No | HWiNFO | Yes (needs admin + PawnIO) |
| Startup-folder autostart entries | No | Autoruns | Yes |
| Per-process GPU % | Limited | — | Yes |
| Services start/stop/restart, users, run dialog | Yes | — | Yes |

## Honest limits

- **Needs admin:** the per-process Netzwerk column (ETW session). Without elevation the column
  stays at the real app's zero-value text — no numbers are ever invented.
- **Needs admin + [PawnIO](https://pawnio.eu):** temperatures, power, and fans via
  LibreHardwareMonitor. Without them the Leistung page shows a banner and simply omits sensor
  data; CPU, memory, disk, network, and GPU graphs still work.
- **Deliberately open** (see `docs/plan-tm2-real.md`, "Bewusst offen"): Scheduled Tasks as an
  autostart source, startup-impact measurement (stays "Nicht gemessen" — the real app uses boot
  traces), and TaskLens.App consuming the new services.

No Microsoft assets are used — no logos, product icons, or artwork; the name and app icon are its
own. The resemblance is layout homage only (nav labels, column names, Mica).

## Build & run

Taskmanager2 is WinUI 3 — it builds and runs on Windows only:

```
dotnet build TaskLens.sln -c Release
dotnet run --project src/Taskmanager2.App -c Release
```

To judge fidelity, open the real Windows Task Manager side by side and compare page by page —
spacing, fonts, colour tints, behaviour. Anything that differs is the next fidelity round.

On Linux only the shared core and its tests build:

```
dotnet build TaskLens.Linux.slnf -c Release
dotnet test tests/TaskLens.Core.Tests -c Release --no-build
dotnet format TaskLens.Linux.slnf --verify-no-changes
```

## TaskLens.App

The repo also contains **TaskLens** (`src/TaskLens.App`), the original sensor-focused task manager
built on the same `TaskLens.Core` pipeline. Prebuilt self-contained zips (win-x64, win-arm64) with
SHA-256 checksums are published on the [Releases](../../releases) page for every `vX.Y.Z` tag; a
[winget](packaging/winget) manifest is maintained. `.github/workflows/release.yml` publishes on
`v*` tags. It does not yet consume the newer Taskmanager2 services (see "Honest limits").
