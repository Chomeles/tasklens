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
