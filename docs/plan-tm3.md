# Taskmanager2 — Parity Plan (tm3 series)

Follow-up to [plan-tm2.md](plan-tm2.md). The tm2 series delivered the seven pages in the Windows 11
Task Manager layout, but **read-only by design** — no end task, no autostart toggling, no service
control, and a metrics model of exactly three values. That made it a density satire; the actual
goal is a task manager that matches the real one **visually and functionally** and then exceeds it.
The tm3 series closes the gap.

**Principle change over plan-tm2 §2:** Taskmanager2 may now mutate the system. Every mutating
capability gets its own small Core interface whose failure modes (access denied, process gone,
service refuses) are modelled as result data — never as exceptions reaching a view. The
`requireAdministrator` manifest from tm2-01 already provides the rights.

## 1. Architecture (delta only)

```
src/
  TaskLens.Core/
    Services/                     #   + IProcessActionService, INetworkMetricsService,
                                  #     IStartupManager, IServiceControl, ISessionActions
                                  #   ~ ISystemMetricsService: SystemMetrics grows memory
                                  #     composition, commit/cache/pools, counts, uptime
    ViewModels/                   #   ~ Tm2* VMs gain commands + detail panels; new
                                  #     Tm2NetworkViewModel etc. — all fake-backed Linux tests
  Taskmanager2.App/
    Services/                     #   + WinProcessActionService, mutating counterparts of the
                                  #     read-only tm2 services, network/disk PDH sampling
```

Everything else (link-compile from TaskLens.App, strict MVVM, x:Bind + wiring-only code-behind,
one task = one PR, conventional commits) is unchanged from plan.md/plan-tm2.md.

## 2. Gates (unchanged)

```
dotnet build TaskLens.Linux.slnf -c Release
dotnet test tests/TaskLens.Core.Tests -c Release --no-build --logger "console;verbosity=minimal"
dotnet format TaskLens.Linux.slnf --verify-no-changes
```

Windows CI (full-sln build + format) must stay green on `main` for every task.

## 3. Task list

- [x] tm3-01-process-actions — **Task beenden / Prozessstruktur beenden**: Core
  `IProcessActionService` (`Terminate(pid, entireTree)` → `ProcessActionResult`), selection +
  `EndTask`/`EndTree` commands and error surface on `Tm2ProcessListViewModel`;
  `WinProcessActionService` on `Process.Kill(entireProcessTree)`; Prozesse page gets single
  selection, row context menu, the top-right „Task beenden" button and an error InfoBar; accept:
  command/selection/error logic fake-tested on Linux, Windows CI green.
- [x] tm3-02-run-and-process-extras — „Neuen Task ausführen" dialog (open, optional „Mit
  Administratorrechten"), context-menu extras: Priorität setzen (`PriorityClass`), Effizienzmodus
  (EcoQoS via `SetProcessInformation`), Dateipfad öffnen (`QueryFullProcessImageName` +
  `explorer /select`); accept: VM logic Linux-tested, actions degrade to result data.
- [x] tm3-03-performance-details — grow `SystemMetrics` (committed/limit, cached, paged/non-paged
  pool, compressed, hardware-reserved, process/thread/handle counts, uptime; static RAM facts:
  speed, slots, form factor) via `GetPerformanceInfo`/`GlobalMemoryStatusEx`/WMI once; Leistung
  page gets the real TM detail panels for CPU (Auslastung, Geschwindigkeit, Prozesse, Threads,
  Handles, Betriebszeit, Basistakt, Kerne/logische Prozessoren) and Arbeitsspeicher incl. the
  Speicherzusammensetzung bar; accept: composition/formatting Linux-tested.
- [x] tm3-04-network (Adapter-Raten via NetworkInterface-Counter; Per-Prozess-ETW weiter offen) — `INetworkMetricsService` (per-adapter send/receive B/s, adapter kind
  Ethernet/WLAN, link speed, addresses); Ethernet/WLAN entries in the Leistung rail with
  send/receive graphs and detail panel; accept: rate computation + adapter grouping Linux-tested.
- [x] tm3-05-disk-gpu-details (ehrliches Subset: Lese-/Schreibrate + Kapazität; Aktivzeit/Antwortzeit brauchen PDH-Disk-Counter, offen) — per-physical-disk metrics (active time %, read/write B/s, response
  time via PDH `PhysicalDisk`; capacity, kind SSD/HDD) with one rail entry per disk; GPU panel
  gains dedicated/shared memory (PDH GPU Adapter Memory); accept: VM logic Linux-tested.
- [x] tm3-06-startup-manage — enable/disable via `StartupApproved\Run` registry values, state
  column reflects it, Startauswirkung column (measured values are SRUM-gated — show TM-style
  „Nicht gemessen" where absent); context menu; accept: toggle state machine Linux-tested.
- [x] tm3-07-services-control — Starten/Beenden/Neu starten via `ServiceController` with manage
  rights requested per-operation (query-only catalog stays for listing), result-data errors,
  context menu; accept: command/error logic Linux-tested.
- [x] tm3-08-users-actions — Verbindung trennen / Abmelden (`WTSDisconnectSession`/
  `WTSLogoffSession`), expandable per-user process grouping (join over the process list by
  session id); accept: grouping + command logic Linux-tested.
- [x] tm3-09-processes-parity — Apps/Hintergrundprozesse grouping (visible top-level window ⇒
  App), app icons (`SHGetFileInfo`), Status column (angehalten/Effizienzmodus), publisher column;
  accept: grouping rules Linux-tested, icon loading Windows-only code.
- [x] tm3-10-shell-polish — per-page command header („Neuen Task ausführen" on every page like the
  real TM), Einstellungen page wired to the existing `SettingsViewModel`, graph accent colors per
  category, window/taskbar icon pass; accept: gates green, README updated (satire disclaimer
  replaced by feature comparison table).

## 4. Definition of Done

- All 10 boxes ticked, each merged individually with green Linux gates + green Windows CI.
- `TaskLens.Core` still has zero Windows dependencies; every new interface is fake-covered.
- Feature parity checklist vs. the Windows 11 Task Manager holds on a real machine: end task,
  run new task, RAM/CPU/disk/network detail panels, autostart toggling, service control, user
  actions, process grouping with icons.
- Deferred (tracked, on demand): per-process network column (ETW cost), NPU rail entry (PDH
  availability varies), SRUM-based Startauswirkung measurements, packaging.
