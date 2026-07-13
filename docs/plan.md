# TaskLens — Implementation Plan

Executable plan derived from [research.md](research.md). All technology choices are fixed there;
this document only sequences the work. Stack: .NET 8, Windows App SDK 2.2.0 (WinUI 3, unpackaged,
`requireAdministrator`), LibreHardwareMonitorLib 0.9.6 (PawnIO), NtQuerySystemInformation,
PDH `GPU Engine` counters, CommunityToolkit.Mvvm 8.4.2, ScottPlot.WinUI + hand-rolled Polyline sparklines.

## 1. Architecture

```
TaskLens.sln                      # everything (built on windows-latest CI)
TaskLens.Linux.slnf               # solution filter: Core + Core.Tests only (built/tested on Linux)
src/
  TaskLens.Core/                  # net8.0 — ZERO Windows dependencies
    Models/                       #   ProcessSample, SensorReading, SensorKind, SystemSnapshot,
                                  #   ProcessDelta, HistoryBuffer<T>, SensorAvailability
    Services/                     #   interfaces + pure logic:
                                  #     IProcessEnumerator      (raw per-tick process rows)
                                  #     ISensorService          (sensor tree snapshot + availability)
                                  #     IGpuProcessService      (pid -> gpu% map)
                                  #     ISystemMetricsService   (total CPU/RAM/disk/net)
                                  #     IDispatcher             (Post(Action) — UI-thread marshal)
                                  #     ISettingsStore          (get/set typed settings)
                                  #     SamplingEngine          (PeriodicTimer loop, composes the
                                  #       services into one SystemSnapshot per tick, computes
                                  #       CPU%/IO deltas keyed by (pid,startTime), raises event)
    ViewModels/                   #   CommunityToolkit.Mvvm; live HERE so they unit-test on Linux.
                                  #   Receive snapshots via IDispatcher.Post — never touch WinUI types.
tests/
  TaskLens.Core.Tests/            # net8.0 xUnit; deterministic fakes for every Core interface
                                  #   (FakeProcessEnumerator, FakeSensorService, SyncDispatcher,
                                  #    ManualClock) — no timers, no threads in tests.
src/
  TaskLens.App/                   # net8.0-windows10.0.19041.0, WinUI 3, WinAppSDK 2.2.0.
                                  # NOT in TaskLens.Linux.slnf; validated only by Windows CI.
    Views/                        #   Shell (NavigationView), ProcessesPage, SensorsPage,
                                  #   DetailsPage (ScottPlot), SettingsPage; x:Bind to Core VMs.
    Services/                     #   Windows implementations of Core interfaces:
                                  #     NtProcessEnumerator     (NtQuerySystemInformation P/Invoke,
                                  #                              Process.GetProcesses() fallback)
                                  #     LhmSensorService        (LibreHardwareMonitorLib, dedicated
                                  #                              sampling thread — Computer not thread-safe)
                                  #     PdhGpuProcessService    (one long-lived PDH wildcard query)
                                  #     WinSystemMetricsService (NT CPU deltas, GlobalMemoryStatusEx, PDH disk)
                                  #     DispatcherQueueDispatcher, JsonSettingsStore
```

Key principle: **every Windows API sits behind a Core interface.** Core has no Windows package
references and compiles on Linux; the App project is pure composition (DI wiring in `App.xaml.cs`
via `Microsoft.Extensions.DependencyInjection`) plus thin Views plus interface implementations.
All sampling/aggregation/formatting logic that can be pure lives in Core and is covered by tests
running against fakes. Data flow per tick: SamplingEngine (background thread) builds one immutable
`SystemSnapshot` → one `IDispatcher.Post` per tick → ViewModels apply it (batch update, no
per-counter notifications). Sensor absence (no admin / no PawnIO / VM) is modelled as data
(`SensorAvailability`), not exceptions — ViewModels degrade gracefully by design.

## 2. Conventions

- C# 12, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, file-scoped namespaces.
- `.editorconfig` at repo root is the single style authority, enforced by `dotnet format --verify-no-changes` in CI.
- MVVM strictly: no logic in code-behind — `*.xaml.cs` contains only `InitializeComponent()` and
  x:Bind wiring; everything observable/commandable lives in Core ViewModels
  (`[ObservableProperty]`/`[RelayCommand]` source generators, classic field syntax for C# 12).
- DI via `Microsoft.Extensions.DependencyInjection`; constructor injection only, no service locator
  outside `App.xaml.cs` composition root. Tests build their own `ServiceCollection` with fakes.
- P/Invoke: `LibraryImport` source generators where possible, all native structs in one
  `Interop/` folder per service, XML-doc the source of each struct layout.
- Conventional commits (`feat:`, `fix:`, `docs:`, `test:`, `chore:`); one task = one branch
  `task/<id>` = one PR to `main`; every merge keeps all gates green.
- MPL-2.0 notice for LibreHardwareMonitorLib in `NOTICE` (see research §2).

## 3. Gates (run locally on this Linux box, copy-paste)

```
cd <repo-or-worktree-root>
dotnet build TaskLens.Linux.slnf -c Release
dotnet test tests/TaskLens.Core.Tests -c Release --no-build --logger "console;verbosity=minimal"
dotnet format TaskLens.Linux.slnf --verify-no-changes
```

All three must exit 0 before any merge. The WinUI App project is excluded from the Linux filter
and is validated by GitHub Actions `windows-latest` CI (`dotnet build TaskLens.sln -c Release`
with the same format check); Windows CI must also be green on `main`. Local box has .NET SDK 8.0.4xx.

## 4. Task list

Each task is small, independently mergeable in order, and lands with green Linux gates
(worktree: `git worktree add .worktrees/<id> -b task/<id>`). Tasks 01–06 are fully
buildable/testable on this Linux box; 07–16 touch the App project (Windows CI validates the
Windows side) but keep any new logic in Core under Linux tests.

- [x] 01-solution-scaffold — Create Core/App/Tests projects, `TaskLens.sln`, `TaskLens.Linux.slnf`, `.editorconfig`, `NOTICE`, and both GitHub Actions workflows (ubuntu: the 3 gates; windows: full sln build); accept: all 3 Linux gates pass, both workflows are valid YAML.
- [x] 02-core-models — Add immutable Models (`ProcessSample`, `SensorReading`, `SensorKind`, `SensorAvailability`, `SystemSnapshot`, `Settings`) with unit tests for equality/invariants; accept: gates green, models have zero service dependencies.
- [x] 03-sampling-engine — `SamplingEngine` + `IDispatcher`/`IClock` + service interfaces in Core: per-tick snapshot composition, CPU%/IO-rate deltas keyed by `(pid, startTime)` (PID-reuse safe), configurable interval; accept: deterministic tests with fakes/ManualClock cover delta math, PID reuse, first-tick behavior.
- [x] 04-process-list-viewmodel — `ProcessListViewModel`: sort (any column, stable), filter by name, aggregate totals row, batch snapshot apply without collection churn; accept: tests cover sort/filter/update-in-place semantics via SyncDispatcher.
- [x] 05-sensor-viewmodel — `SensorsViewModel` + graceful-degradation logic: group by hardware, map `SensorAvailability` to banner states (no admin / no PawnIO / no sensors-VM), unit formatting (°C/W/RPM); accept: tests cover all availability states and formatting.
- [x] 06-history-ring-buffers — Fixed-capacity `HistoryBuffer<T>` ring buffer in Core + per-process/per-sensor history retention in the engine, sparkline point downsampling; accept: tests cover wrap-around, capacity, downsample output.
- [ ] 07-app-shell — `TaskLens.App` becomes a real WinUI 3 app: `App.xaml.cs` DI composition root, NavigationView shell with empty pages, `DispatcherQueueDispatcher`; accept: Linux gates unaffected, Windows CI builds and app launches with stub data.
- [ ] 08-process-page-view — `ProcessesPage` XAML bound (x:Bind) to `ProcessListViewModel`: sortable columns, filter box, totals; code-behind wiring only; accept: Windows CI green, page renders fake-backed data via a debug fake registration.
- [ ] 09-sensors-page-view — `SensorsPage` with hardware groups, per-sensor Polyline sparklines fed from `HistoryBuffer`, degradation banner; accept: Windows CI green, sparkline point-mapping helper unit-tested in Core.
- [ ] 10-windows-process-service — `NtProcessEnumerator` P/Invoke (`SystemProcessInformation`: name, PID, CPU times, working set, IO counters) + `Process.GetProcesses()` fallback behind `IProcessEnumerator`; accept: buffer-parsing logic isolated and unit-tested against captured byte fixtures on Linux, Windows CI smoke-runs the real path.
- [ ] 11-windows-sensor-service — `LhmSensorService` on LibreHardwareMonitorLib 0.9.6: dedicated sampling thread, `Computer` open/update/close lifecycle, maps LHM tree to `SensorReading`/`SensorAvailability`; accept: mapping logic unit-tested in Core, Windows CI runs it expecting the VM "no sensors" state.
- [ ] 12-gpu-pdh-service — `PdhGpuProcessService`: one long-lived PDH query on `\GPU Engine(*)\Utilization Percentage`, instance-name `pid_` parsing, per-PID max-across-engines aggregation; accept: instance-name parser + aggregation unit-tested in Core, missing-counter systems degrade to empty map.
- [ ] 13-details-page-scottplot — `DetailsPage` (per-process + system history) using ScottPlot.WinUI, one `Refresh()` per tick, animations off; `DetailsViewModel` in Core; accept: VM series-building tested on Linux, Windows CI green.
- [ ] 14-settings-persistence — `SettingsPage` + `JsonSettingsStore` (`%LocalAppData%\TaskLens\settings.json`): refresh interval, temperature unit, CPU% normalization mode; live-applied via `ISettingsStore`; accept: store round-trip + corrupt-file recovery tested in Core with temp-dir path.
- [ ] 15-elevation-manifest — `app.manifest` with `requireAdministrator`, `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`, first-run banner detecting missing PawnIO with install hint; accept: detection logic unit-tested, Windows CI builds the manifested exe.
- [ ] 16-release-packaging — Release workflow: self-contained x64+arm64 zips with SHA-256 checksums on tag push, winget manifest (zip installer type), README with feature list + screenshots placeholders; accept: workflow dry-runs green, `dotnet publish` commands documented and CI-executed.

## 5. Definition of Done

- All 16 task checkboxes ticked; every task merged to `main` individually with green gates.
- Linux gates (build, test, format) green on `main`; GitHub Actions ubuntu + windows workflows both green.
- `TaskLens.Core` has zero Windows dependencies; every Windows API call sits behind a tested Core interface.
- README: what/why, feature table vs Task Manager/HWiNFO, screenshots placeholder section, build
  instructions for both OSes, PawnIO/admin explanation; MIT `LICENSE` + `NOTICE` (MPL-2.0 attribution) present.
- App is fully useful without admin/PawnIO (processes, GPU, RAM, disk) and states clearly what elevation unlocks.
- Release workflow produces versioned x64/arm64 zips with checksums; winget manifest ready to submit.
- `v0.1.0` tag ready on `main`.
- Known deferrals tracked as issues: per-process network via ETW (v2, research §3), net10.0 TFM bump
  before .NET 8 EOS 2026-11-10 (research §1), SignPath OSS code signing (research §8).
