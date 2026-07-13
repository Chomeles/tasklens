# TaskLens — Projektabschluss

Erweiterter Windows-Taskmanager: Prozesse plus die Werte, für die man sonst Extra-Tools braucht
(CPU-/Mainboard-/GPU-Temperaturen, Watt, Lüfter-RPM, per-Prozess-GPU%), als unpackaged WinUI-3-App.

## Endstand (13.07.2026)

- **16/16 Tasks** aus [plan.md](plan.md) gemerged, 53 Commits auf `main`.
- **Gates grün:** `dotnet build` 0 Warnungen/Fehler, **199/199 Tests**, `dotnet format` clean; Windows-CI (voller sln-Build) grün.
- ~4.500 Zeilen C# in `src/` + `tests/`, keine offenen Worktrees/Branches.

## Was gebaut wurde

- **TaskLens.Core** (Linux-testbar, null Windows-Referenzen): SamplingEngine (PID-reuse-sichere CPU/IO-Deltas),
  HistoryBuffer + Sparkline-Downsampling, alle ViewModels (Prozesse: Sort/Filter/Totals; Sensoren: Gruppierung,
  °C/°F/W/RPM-Formatierung, Degradation-Banner; Details; Settings), JsonSettingsStore mit Korrupt-Datei-Recovery.
- **TaskLens.App** (WinUI 3, MVVM strikt, DI-Composition-Root): NtProcessEnumerator (NtQuerySystemInformation
  P/Invoke + Fallback), LhmSensorService (LibreHardwareMonitorLib 0.9.6/PawnIO, eigener Sampling-Thread),
  PdhGpuProcessService (GPU-Engine-Counter wie der echte Taskmanager), NavigationView-Shell mit
  Prozess-/Sensor-/Detail-/Settings-Seiten, `requireAdministrator`-Manifest + PawnIO-Hinweis-Banner.
- **Release:** Tag-Push baut self-contained x64+arm64-Zips mit SHA-256, winget-Manifest, README.

## Ehrliche Grenzen

- Auf dieser Linux-Box lief nur Core (Build+Tests) und die Windows-CI; **die App wurde noch nie auf echter
  Windows-Hardware gestartet** — Sensor-Pfade (LHM/PawnIO, PDH) brauchen einen manuellen Smoke-Test vor dem ersten Release-Tag.
- Sensorwerte erfordern Adminrechte + installiertes PawnIO; ohne beides degradiert die App per Banner (by design).

## Anschlussprojekt

**Taskmanager2** (Satire): 1:1-Optik des Windows-11-Taskmanagers, UI-only über TaskLens.Core,
alle Zusatzwerte an den „richtigen" Stellen einsortiert. Eigener Plan folgt in docs/plan-tm2.md.
