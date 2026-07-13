# Taskmanager2 — Implementation Plan (Satire)

Follow-up to [plan.md](plan.md). A SECOND app project in the same repo that clones the Windows 11
Task Manager look 1:1 — left NavigationView (Prozesse, Leistung, App-Verlauf, Autostart-Apps,
Benutzer, Details, Dienste), Mica backdrop, same column/layout feel — but overloaded with every
value TaskLens.Core already provides, placed where a user would expect them: temperature/watt/fan
columns directly in the process list, sparklines in cells, per-process GPU%, a Leistung page
stuffed with all sensor groups. **The joke is density, not fake data** — every displayed value is
real and comes from the existing sampling pipeline.

## 1. Architecture

```
TaskLens.sln                      # + Taskmanager2.App (built on windows-latest CI, no workflow change)
TaskLens.Linux.slnf               # UNCHANGED: Core + Core.Tests only
src/
  TaskLens.Core/                  # grows only view-composition + thin new read-only interfaces:
    Models/                       #   + ServiceEntry, StartupItem, UserSession (immutable records)
    Services/                     #   + IServiceCatalog, IStartupItemSource, IUserSessionSource
                                  #   (read-only; absence/access-denied modelled as data, not exceptions)
    ViewModels/                   #   + Tm2ProcessListViewModel/Tm2ProcessRowViewModel (JOIN over the
                                  #     existing ProcessListViewModel — no reimplemented sort/filter),
                                  #     Tm2PerformanceViewModel, Tm2AppHistoryViewModel,
                                  #     Tm2ServicesViewModel, Tm2StartupViewModel, Tm2UsersViewModel
  TaskLens.App/                   # UNCHANGED (source of link-compiled Windows services)
  Taskmanager2.App/               # net8.0-windows10.0.19041.0, WinUI 3, WinAppSDK 2.2.0, unpackaged,
                                  # requireAdministrator (same manifest recipe as TaskLens.App).
    Views/                        #   Shell (NavigationView, Mica) + the 7 pages; x:Bind to Core VMs.
    Services/                     #   ONLY the three new thin read-only impls:
                                  #     ScmServiceCatalog       (ServiceController)
                                  #     RegistryStartupSource   (Run keys + startup folders)
                                  #     WtsUserSessionSource    (wtsapi32 P/Invoke, Interop/ pattern)
                                  #   Everything else is link-compiled from TaskLens.App/Services
                                  #   (<Compile Include>, the existing SmokeTests precedent):
                                  #   NtProcessEnumerator, LhmSensorService, PdhGpuProcessService,
                                  #   WinSystemMetricsService, DispatcherQueueDispatcher, Interop/*,
                                  #   Views/SparklineRender.cs.
tests/
  TaskLens.Core.Tests/            # + fakes: FakeServiceCatalog, FakeStartupItemSource,
                                  #   FakeUserSessionSource; tests for every new VM/model.
```

Key principles carried over from plan.md, plus:

- **UI-only project.** Taskmanager2.App contains Views, the DI composition root, and the three thin
  new service impls — nothing else. All logic (joins, sorting, formatting, history) lives in Core
  and tests on Linux. No logic is duplicated from TaskLens.App: shared Windows services are
  link-compiled, not copied. <!-- ponytail: link-compile, extract a TaskLens.Windows lib only if a third consumer appears -->
- **Density satire = real data in absurd places.** Sensor values in process rows are the live
  system-wide readings (CPU-Paket-Temp, Package-Watt, Lüfter-RPM) stamped onto every row — honest,
  just gloriously misplaced. Per-row sparklines reuse `HistoryBuffer<T>` + `Sparkline.MapPoints`.
- **App-Verlauf without SRUM.** Real Windows app history needs the SRUM database — not worth it.
  The page shows per-process cumulative CPU time (`ProcessSample.TotalCpuTime`) and cumulative IO
  bytes (already in every snapshot) since app start, with a one-line InfoBar admitting it.
- **Branding.** Name "Taskmanager2", own simple generated icon, NO Microsoft logos/assets/product
  icons anywhere. Layout mimicry (nav labels, column names, Mica) is fine.
- **Dependencies.** No new NuGet packages except `System.ServiceProcess.ServiceController`
  (unavoidable for the Dienste page on .NET 8; Microsoft-maintained BCL extension).
  `Microsoft.Win32.Registry` APIs are in-box on `net8.0-windows`. No ScottPlot — plain `Polyline`
  via the existing helper, as in TaskLens.

## 2. Conventions

Identical to [plan.md §2](plan.md) (C# 12, `.editorconfig`, strict MVVM, DI, `LibraryImport`
Interop pattern, conventional commits, one task = one branch `task/<id>` = one PR). Additions:

- Nav/page/column labels in German, matching the Windows 11 Task Manager wording (Prozesse,
  Leistung, App-Verlauf, Autostart-Apps, Benutzer, Details, Dienste).
- New Core interfaces are read-only by design: no service start/stop, no autostart toggling, no
  session disconnect. Taskmanager2 observes; it never mutates the system.
- Access-denied / unavailable data (e.g. SCM query without rights) degrades to an availability
  state in the VM (same pattern as `SensorAvailability`), never to an exception in the view.

## 3. Gates (unchanged, run locally on this Linux box, copy-paste)

```
cd <repo-or-worktree-root>
dotnet build TaskLens.Linux.slnf -c Release
dotnet test tests/TaskLens.Core.Tests -c Release --no-build --logger "console;verbosity=minimal"
dotnet format TaskLens.Linux.slnf --verify-no-changes
```

All three must exit 0 before any merge. Taskmanager2.App is NOT in the Linux filter; it is
validated by the existing Windows CI (`dotnet build TaskLens.sln -c Release` + format check),
which picks it up automatically once tm2-01 adds the project to `TaskLens.sln` — **no workflow
edits required**. Windows CI must stay green on `main` for every task.

## 4. Task list

Each task is small, independently mergeable in order, and lands with green Linux gates
(worktree: `git worktree add .worktrees/<id> -b task/<id>`). One task per page-group, not per
control. Max 8 tasks — hard cap.

- [x] tm2-01-app-scaffold — Create `src/Taskmanager2.App` (WinUI 3, unpackaged, Mica backdrop, `requireAdministrator` manifest, own simple icon, no Microsoft assets), add to `TaskLens.sln` only; NavigationView shell with all 7 nav items and empty pages; DI composition root wiring the link-compiled TaskLens.App services (`<Compile Include>` per SmokeTests precedent) + debug fake registrations; accept: Linux gates untouched, Windows CI builds full sln, app launches with stub data.
- [x] tm2-02-process-join-viewmodel — Core: `Tm2ProcessRowViewModel`/`Tm2ProcessListViewModel` as a thin join layer over the existing `ProcessListViewModel` (reuse its sort/filter/totals — do not reimplement): per-row CPU sparkline series (`HistoryBuffer` accumulated in `ApplySnapshot`), system-wide CPU-Temp/Package-Watt/Fan-RPM stamped onto every row from `SystemSnapshot.Sensors`, GPU% passed through, new sortable columns; accept: Linux tests via fakes cover join, sensor stamping (incl. sensors-unavailable → empty columns), sparkline series growth.
- [ ] tm2-03-processes-page — Prozesse page XAML in Win11 look: columns Name, PID, Status, CPU %, GPU %, Arbeitsspeicher, Datenträger, **Temperatur, Leistung (W), Lüfter (RPM), Verlauf (sparkline cell)**; bound via x:Bind to `Tm2ProcessListViewModel`, sparkline cells via the link-compiled `SparklineRender` + Core `Sparkline.MapPoints`; code-behind wiring only; accept: Windows CI green, any new point-mapping logic (if any) tested in Core.
- [ ] tm2-04-performance-page — Leistung page: left rail of mini-graph entries (CPU, Arbeitsspeicher, Datenträger, GPU **plus one entry per sensor hardware group** — the overload), main panel with big `Polyline` history graphs and every sensor reading of the selected group; Core: `Tm2PerformanceViewModel` that only composes the existing `SensorsViewModel` groups + `DetailsViewModel`-style system histories (no new sampling logic); accept: composition/selection logic Linux-tested, Windows CI green.
- [ ] tm2-05-app-history-page — App-Verlauf: Core `Tm2AppHistoryViewModel` aggregating per-process cumulative CPU time (`TotalCpuTime`) and cumulative IO read/write bytes from snapshots (data already tracked), name-grouped, sorted by CPU time; page in the Win11 app-history table look with a one-line InfoBar: real app history needs SRUM — this is since app start (satire-compatible); accept: aggregation/grouping Linux-tested, Windows CI green.
- [ ] tm2-06-services-page — Dienste: Core `ServiceEntry` model + `IServiceCatalog` + `Tm2ServicesViewModel` (filter, status grouping Wird ausgeführt/Beendet, sort) with fake-backed Linux tests; thin read-only `ScmServiceCatalog` on `System.ServiceProcess.ServiceController` (the one new NuGet) in Taskmanager2.App; page with columns Name, PID, Beschreibung, Status; accept: gates green, access-denied degrades to availability state, no start/stop anywhere.
- [ ] tm2-07-startup-users-pages — Autostart-Apps + Benutzer in one task: Core `StartupItem`/`UserSession` models + `IStartupItemSource`/`IUserSessionSource` + two thin list VMs with fake-backed Linux tests; Windows impls: `RegistryStartupSource` (HKLM/HKCU `Run` keys incl. Wow6432Node + both startup folders, in-box registry APIs) and `WtsUserSessionSource` (`WTSEnumerateSessions` P/Invoke, Interop/ pattern); pages with Win11 columns (Autostart: Name, Befehl, Quelle, Status; Benutzer: Benutzer, Sitzungs-ID, Status); read-only; accept: gates green, VM logic Linux-tested.
- [ ] tm2-08-details-and-polish — Details page reusing `DetailsViewModel` + the tm2-02 row join (every column, per-process history graphs — max density); final icon, window title/branding pass, README section for Taskmanager2 (what/why/satire disclaimer, screenshot placeholders, explicit "no Microsoft assets, layout homage" note); accept: all gates green, Windows CI green, no release-workflow changes (Taskmanager2 ships from source only). <!-- ponytail: no zip/winget packaging for the satire app — add a publish step only if someone actually asks for a binary -->

## 5. Definition of Done

- All 8 task checkboxes ticked; every task merged to `main` individually with green gates.
- Linux gates green on `main`; ubuntu + windows workflows both green with zero workflow edits
  beyond what tm2-01 needs (expected: none — full-sln build already covers new projects).
- `TaskLens.Core` still has zero Windows dependencies; the three new interfaces are covered by
  fake-backed tests; no logic duplicated between TaskLens.App and Taskmanager2.App (link-compile only).
- Taskmanager2.App contains no Microsoft logos/assets/product icons; name and icon are its own.
- All seven pages show real data from the existing pipeline; the only admitted gap (SRUM) is
  stated in-app in one line.
- Deferred by design (tracked as issues, built only on demand): TaskLens.Windows shared library
  extraction, Taskmanager2 release packaging, service/autostart/user mutation actions.
