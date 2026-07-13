# TaskLens — Technical Research (July 2026)

Research for an open-source WinUI 3 task manager that surfaces what Windows Task Manager hides:
CPU/GPU temperatures, package power (watts), fan speeds, per-process GPU usage, per-process disk I/O.
All version claims verified against live sources in July 2026.

**Build constraint:** development and CI gates run on Linux. The solution is therefore split:

- `TaskLens.Core` (Models, Service interfaces + pure logic, ViewModels) and `TaskLens.Core.Tests` (xUnit)
  target plain `net8.0` — build and test on Linux.
- `TaskLens.App` (WinUI 3 Views + Windows-only service implementations: sensors, PDH, NT APIs)
  targets `net8.0-windows10.0.19041.0` — built only on GitHub Actions `windows-latest`.

---

## 1. Windows App SDK / WinUI 3

**Current stable: Windows App SDK 2.2.0, released 2026-06-09** (before that: 2.1.3 on 2026-05-21,
2.0.1 on 2026-04-29). The 1.x line is superseded; 2.x is the active stable channel.
Source: [Latest Windows App SDK downloads](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) (page updated 2026-06-11).

**.NET target:** C# WinAppSDK 2.x projects target `net8.0-windows10.0.xxxxx`; .NET 8 has been the
minimum since WinAppSDK 1.6 and remains supported by the 2.2.0 NuGet package
([Microsoft.WindowsAppSDK on NuGet](https://www.nuget.org/packages/Microsoft.WindowsAppSdk/),
[versioning overview](https://learn.microsoft.com/en-us/windows/apps/get-started/versioning-overview)).
**Caveat:** .NET 8 *and* 9 reach end of support on **2026-11-10**; .NET 10 is the current LTS
(supported to Nov 2028) — see [.NET blog](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/).
Start on `net8.0`/`net8.0-windows` per the fixed stack, but plan a mechanical TFM bump to
`net10.0` before November 2026; nothing in this design depends on the .NET major version.

**Packaged vs unpackaged:** WinAppSDK apps can ship (a) MSIX-packaged, (b) "packaged with external
location", or (c) fully unpackaged, where the app deploys the Windows App Runtime via the
standalone installer or redistributable
([deployment guide](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/deploy-unpackaged-apps)).
Unpackaged apps set `<WindowsPackageType>None</WindowsPackageType>` and can be distributed as a plain zip.

**Elevation (required for hardware sensors):**

- Elevated **packaged** WinAppSDK apps are supported on **Windows 11 only** — the OS-level support
  never shipped for Windows 10
  ([WindowsAppSDK issue #896](https://github.com/microsoft/WindowsAppSDK/issues/896)).
  MSIX needs the restricted `allowElevation` capability plus
  `requestedExecutionLevel level="requireAdministrator"` in the exe manifest
  ([Advanced Installer on allowElevation](https://www.advancedinstaller.com/allow-elevation-msix-packages.html),
  [Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/1692811/how-to-set-up-winui3-applications-to-run-as-admini)).
- **Unpackaged** WinUI 3 apps elevate the classic way: an `app.manifest` with
  `requestedExecutionLevel level="requireAdministrator"` → normal UAC prompt
  ([WindowsAppSDK discussion #3038](https://github.com/microsoft/WindowsAppSDK/discussions/3038)).
  Early WinAppSDK versions had elevated-mode crashes
  ([microsoft-ui-xaml #3046](https://github.com/microsoft/microsoft-ui-xaml/issues/3046)); resolved in current releases, but elevated mode gets less test coverage than normal mode — test it in CI smoke runs.
- **What real projects do:** hardware tools that need ring-0 access (FanControl,
  Libre Hardware Monitor, OpenRGB) ship **unpackaged** exe/zip and request admin via manifest or
  on-demand. That is the path of least resistance for us.

**Decision:** unpackaged WinUI 3 app, `requireAdministrator` manifest, WinAppSDK 2.2.x,
self-contained WinAppSDK deployment (`WindowsAppSDKSelfContained=true`) so the zip needs no runtime installer.

## 2. LibreHardwareMonitorLib

**Current NuGet version: 0.9.6, released 2026-02-14. License: MPL-2.0.**
Targets .NET Framework 4.7.2, netstandard2.0 and net8.0 (works on net9/net10).
Dependencies: HidSharp, DiskInfoToolkit, RAMSPDToolkit, System.Management, System.IO.Ports.
Source: [LibreHardwareMonitorLib on NuGet](https://www.nuget.org/packages/LibreHardwareMonitorLib/).

**License implications (MPL-2.0 dependency in an MIT app):** MPL-2.0 is *file-level* copyleft.
Consuming the unmodified NuGet package from MIT-licensed code is fine — MPL explicitly permits
combining with proprietary/permissive code ("larger work"). Obligations: keep the MPL notices, and
if we ever *modify* LHM source files, those files (not our app) must be published under MPL-2.0.
Source: [MPL 2.0 FAQ](https://www.mozilla.org/en-US/MPL/2.0/FAQ/). Document this in our NOTICE file.

**Sensor coverage** (see [project README](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)):

- CPU: per-core temperatures, per-core clocks/loads, **package power in watts** (Intel via RAPL
  MSRs, AMD via SMU/RAPL equivalents) — requires MSR access, i.e. the kernel driver.
- GPU: temperature, power, clocks, VRAM, fan — NVIDIA via NVML/NVAPI, AMD via ADL/ADLX, Intel via
  IGCL/D3D. These vendor APIs mostly work *without* admin.
- Motherboard: fan RPM, voltages, chassis temps via SuperIO chips (ITE/Nuvoton) — needs port I/O → driver.
- Storage SMART temps, RAM SPD temps, PSU (some), battery.

**The driver story — WinRing0 is dead, PawnIO is the present:**

- March 2025: Microsoft Defender began flagging the old WinRing0 driver
  (`HackTool:Win32/Winring0` / `VulnerableDriver:WinNT/Winring0`, rooted in CVE-2020-14979 —
  arbitrary ring-0 read/write from user mode), quarantining it and breaking HWiNFO, LHM,
  FanControl, MSI Afterburner and others.
  Sources: [Microsoft support alert](https://support.microsoft.com/en-us/windows/microsoft-defender-antivirus-alert-vulnerabledriver-winnt-winring0-eb057830-d77b-41a2-9a34-015a5d203c42),
  [Slashdot 2025-03-14](https://it.slashdot.org/story/25/03/14/1351225/windows-defender-now-flags-winring0-driver-as-security-threat-breaking-multiple-pc-monitoring-tools),
  [Neowin](https://www.neowin.net/news/windows-1110-is-flagging-winring0-on-your-pc-monitoring-fan-control-apps-heres-why/).
- **PawnIO** (by namazso) is the replacement: a signed kernel driver that executes *signed* Pawn
  bytecode modules exposing narrow IOCTLs (MSR read, SuperIO access) instead of raw port/memory
  access. LHM swapped WinRing0 for PawnIO in
  [PR #1857](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/pull/1857) (merged
  2025-09-16); **LibreHardwareMonitorLib 0.9.6 ships on PawnIO** (release notes: "Update PawnIO
  modules to 2.2", [releases](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases)).
  FanControl migrated the same way (V238+,
  [PawnIO install guide](https://github.com/Rem0o/FanControl.Releases/issues/3480)).
- PawnIO is installed system-wide (separate installer); LHM detects it. Without PawnIO/admin, LHM
  still returns driverless sensors (GPU via NVML, SMART), just not CPU temps/power/fans
  ([LHM discussion #2149](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/discussions/2149)).
  → We must degrade gracefully: show what's available, banner explaining what admin + PawnIO unlocks.

**Usage pattern** (from the [README](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)):

```csharp
var computer = new Computer {
    IsCpuEnabled = true, IsGpuEnabled = true, IsMotherboardEnabled = true,
    IsMemoryEnabled = true, IsStorageEnabled = true
};
computer.Open();                       // once, at startup (slow: enumerates hardware)
computer.Accept(new UpdateVisitor());  // IVisitor calling hardware.Update() each tick
// traverse computer.Hardware[i].Sensors: SensorType.Temperature/.Power/.Fan/.Load, .Value
computer.Close();                      // on exit — releases driver handles
```

Wrap behind an `ISensorService` in Core; the LHM-backed implementation lives in the Windows-only
project. `Computer` is not thread-safe — run open/update/read on one dedicated sampling thread.

## 3. Per-process metrics (no third-party tools)

**Enumeration at 1 s / 300+ processes:**

- `Process.GetProcesses()` allocates a `Process` object per PID and lazily opens handles per
  property; refreshing name+CPU+memory for 300 processes each second is workable but wasteful,
  and `TotalProcessorTime` throws on access-denied processes.
- **`NtQuerySystemInformation(SystemProcessInformation)`** returns one buffer with *every*
  process: name, PID, thread count, kernel/user CPU times, working set/private bytes, **and I/O
  transfer counters** — no per-process handles at all. This is what Process Hacker / System
  Informer use for exactly this reason.
  Sources: [NtQuerySystemInformation docs](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntquerysysteminformation),
  [struct layout reference](https://ntdoc.m417z.com/ntquerysysteminformation),
  [worked example](https://gist.github.com/TheWover/71079af504ba8e056c9ebbe017d288a0).
  Microsoft marks it "subject to change", but the SystemProcessInformation layout has been stable
  for ~20 years and the entire process-tool ecosystem depends on it. **Decision: use it**, with a
  `Process.GetProcesses()` fallback path behind the same `IProcessEnumerator` interface.
- WMI (`Win32_Process`/`Win32_PerfFormattedData`) is an order of magnitude slower — not for a 1 s loop.

**Per-process CPU% done right:** sample `(KernelTime + UserTime)` per PID each tick;
`cpu% = Δcputime / (Δwallclock × logicalCoreCount) × 100` (or × coreCount to get Task-Manager-style
0–100% normalized). Both times come free in the SystemProcessInformation buffer; with
`System.Diagnostics.Process` the equivalent is the delta of
[`Process.TotalProcessorTime`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.totalprocessortime).
Keep a `Dictionary<(pid, startTime), lastCpuTime>` so PID reuse doesn't produce garbage spikes.

**Per-process GPU:** PDH counter set **`GPU Engine`**, counter `Utilization Percentage`, instance
names like `pid_1234_luid_0x..._phys_0_eng_0_engtype_3D`. This is *the same data Task Manager
uses* (WDDM GPU scheduler counters, introduced with the Fall Creators Update):
[DirectX blog — GPUs in the Task Manager](https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/).
Method: one PDH query with wildcard path `\GPU Engine(*)\Utilization Percentage`, collect twice
(rate counter), parse `pid_` out of instance names, aggregate per PID (Task Manager reports max
across engine types; sum-capped-at-100 is also defensible). PDH usage details:
[Microsoft Q&A example](https://learn.microsoft.com/en-us/answers/questions/5641645/how-to-get-the-special-process-gpu-usage-with-the).
Cost note: systems can expose hundreds of engine instances; reuse one `PdhCollectQueryData` query —
do NOT rebuild the query each tick. `GPU Process Memory(pid_*)` gives per-process VRAM
(known quirks: [counter accuracy issue](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/gpu-process-memory-counters-report-wrong-value)).

**Per-process disk I/O:** read `ReadTransferCount`/`WriteTransferCount` deltas — available in the
same SystemProcessInformation buffer (or via `GetProcessIoCounters` per handle). Bytes/sec per
process, same delta technique as CPU. Note: counts all I/O (incl. pipes), matching Task Manager's
"I/O" columns rather than strictly disk.

**Per-process network:** there is **no polling API**. Attribution requires an ETW real-time
session (`Microsoft-Windows-TCPIP` or kernel network keyword) consumed via the
[TraceEvent library](https://github.com/Microsoft/dotnet-samples/blob/master/Microsoft.Diagnostics.Tracing/TraceEvent/docs/TraceEvent.md),
correlating events to PIDs — admin-only, one system-wide kernel session, non-trivial lifetime
management ([overview](https://en.ittrip.xyz/windows10/win10-tcp-bytes-monitor)).
**Out of scope for v1**; design `IProcessNetworkService` interface now, ship ETW implementation in v2.
(`GetExtendedTcpTable` can map connections→PID for a "connections" column cheaply, without byte rates.)

## 4. System-wide metrics

- **CPU total/per-core:** PDH `\Processor Information(*)\% Processor Utility` is what Task Manager
  shows on modern CPUs (frequency-normalized); `% Processor Time` is the classic busy-time metric.
  Cheapest driverless alternative: `NtQuerySystemInformation(SystemProcessorPerformanceInformation)`
  deltas — one syscall for all cores, no PDH machinery.
- **RAM:** `GlobalMemoryStatusEx` P/Invoke — one cheap call, total/available/committed
  ([docs](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex)).
- **Disk throughput:** PDH `\PhysicalDisk(*)\Disk Read Bytes/sec`, `Disk Write Bytes/sec`, `% Idle Time`.
- **Network throughput:** `NetworkInterface.GetIPStatistics()` byte deltas (pure .NET, cheap,
  [docs](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.networkinterface)),
  or PDH `\Network Interface(*)\Bytes Total/sec`.
- `System.Diagnostics.PerformanceCounter` (NuGet `System.Diagnostics.PerformanceCounter`,
  Windows-only) is a usable PDH wrapper but instantiation is slow and instance-name handling is
  clumsy; direct PDH P/Invoke with one long-lived query is cheaper and is required for GPU Engine
  wildcards anyway. **Decision: one PDH query object for GPU/disk; NT API for CPU; P/Invoke for RAM.**
  WMI: avoid for anything sampled.

## 5. CommunityToolkit.Mvvm

**Current version: 8.4.2** ([NuGet](https://www.nuget.org/packages/CommunityToolkit.Mvvm),
[docs](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/)). Runs on plain `net8.0` —
ViewModels build and unit-test on Linux.

- Use `ObservableObject` + `[ObservableProperty]` + `[RelayCommand]` source generators (8.4 added
  partial-property syntax for `[ObservableProperty]` with C# 13; the classic field syntax works on C# 12/net8.0).
- **DI:** plain `Microsoft.Extensions.DependencyInjection`; build a `ServiceProvider` in `App.xaml.cs`
  and expose `App.Services` (or `Ioc.Default`). Register Core interfaces → Windows implementations
  there; tests register fakes. No HostBuilder needed for a desktop app.
- **Threading pattern:** sensor/process sampling runs on a background loop
  (`PeriodicTimer` at 1 s). WinUI 3 UI objects are single-threaded; marshal with
  [`DispatcherQueue`](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.dispatching.dispatcherqueue):
  capture `DispatcherQueue.GetForCurrentThread()` at ViewModel construction (UI thread), sampler
  raises snapshot events, ViewModel does `_dispatcherQueue.TryEnqueue(() => ApplySnapshot(s))`.
  Keep Core free of WinUI types by hiding it behind an `IDispatcher { void Post(Action a); }`
  interface with a synchronous test implementation. Batch: one snapshot object per tick, one
  enqueue per tick — not one per counter — or ObservableCollection churn will dominate CPU.

## 6. Charting for live 1 s graphs

- **LiveCharts2**: `LiveChartsCore.SkiaSharpView.WinUI` **2.0.4**, GA, Skia-based, MIT
  ([NuGet](https://www.nuget.org/packages/LiveChartsCore.SkiaSharpView.WinUI/),
  [WinUI 3 walkthrough](https://xamlbrewer.wordpress.com/2023/12/04/displaying-charts-in-winui3-with-livecharts2/)).
  Pretty, animated, good MVVM binding; animations + Skia invalidation cost CPU with many
  simultaneous charts (disable `EasingFunction`/animations for live views).
- **ScottPlot**: `ScottPlot.WinUI` **5.1.58** (updated 2026-03-29), MIT, explicitly
  performance-focused, WinUI quickstart available
  ([NuGet](https://www.nuget.org/packages/ScottPlot.WinUI/), [quickstart](https://scottplot.net/quickstart/winui/)).
  Imperative API (call `Refresh()`), fewer MVVM niceties, excellent for streaming data.
- **Hand-rolled**: a XAML `Polyline` (or Win2D canvas) fed 60 points is trivially cheap and is the
  standard trick for per-row sparklines.

**Recommendation:** hand-rolled `Polyline` sparklines for the process list and small tiles
(zero dependency, lowest overhead at 1 Hz), **ScottPlot.WinUI for the big detail graphs**
(CPU/GPU/temp history) with animations irrelevant and `Refresh()` called once per tick.
LiveCharts2 is the fallback if we later want gauges/interactive tooltips.

## 7. Competitors and our niche

[Task Manager](https://learn.microsoft.com/en-us/windows/whats-new/) shows per-process GPU and a
single GPU temp but no CPU temps, no watts, no fans.
[Process Explorer](https://learn.microsoft.com/en-us/sysinternals/downloads/process-explorer) and
[System Informer](https://github.com/winsiderss/systeminformer) (Process Hacker's successor) are
superb process tools but expose no hardware sensors and have dated, expert-oriented UIs.
[HWiNFO](https://www.hwinfo.com/) and
[Libre Hardware Monitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) are superb
sensor tools with no process management (and HWiNFO is closed-source). **Our niche:** the union —
one modern, open-source WinUI 3 app where the process list and the sensor panel live together
("which process is making the fans spin, and how many watts is it costing"), replacing the
three-app stack (Task Manager + HWiNFO + Process Explorer) for everyday power users.

## 8. Distribution

- **GitHub Releases** with a portable **zip** (unpackaged, self-contained WinAppSDK) as the primary
  artifact. MSIX is a poor fit for v1: elevation requires the restricted `allowElevation`
  capability and Windows 11, and the PawnIO kernel driver can't be delivered inside MSIX anyway.
- **winget:** zip and portable installer types are supported (zip since winget 1.5); submit a
  manifest PR to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) with
  [wingetcreate](https://github.com/microsoft/winget-create); automated validation runs on the PR
  ([manifest docs](https://learn.microsoft.com/en-us/windows/package-manager/package/repository),
  [installer schema](https://github.com/microsoft/winget-pkgs/blob/master/doc/manifest/schema/1.6.0/installer.md)).
- **Code signing reality:** unsigned exes trigger SmartScreen "unrecognized app" until reputation
  accrues — annoying but standard for OSS (LHM/FanControl live with it).
  [SignPath.io offers free signing for OSS projects](https://signpath.org/about); Azure Trusted
  Signing is the paid route. Ship v1 unsigned with SHA-256 checksums in release notes, apply for
  SignPath OSS certification, revisit later. Never let CI produce unsigned *and* signed artifacts
  with the same version string.

## 9. Name check

Working name **TaskLens** — collisions found: an
[Obsidian task-dashboard plugin](https://github.com/nightaqua/tasklens) and a
[small personal todo CLI](https://github.com/mattdm/tasklens). Both are unrelated todo tools with
tiny footprints; no well-known Windows software or obvious trademark uses the name.

Candidates checked:

| Name | Collision status |
|---|---|
| **TaskLens** (working) | Minor: Obsidian plugin, hobby todo apps. No system-tool collision. |
| **ProcScope** | Collides in-domain: a [Linux eBPF process tracer](https://github.com/Mutasem-mk4/procscope). Avoid. |
| **SysLens** | Several small [Linux/Python CLI monitors](https://github.com/samikshapatel27/syslens) use it. Weak. |
| **TaskScope** | No notable Windows app or repo found — cleanest of the alternatives. |

**Recommendation: keep TaskLens.** The collisions are low-profile, different-category tools; the
name says exactly what the app is. TaskScope is the backup if a trademark issue surfaces.

## 10. Risks

- **Kernel driver dependency:** PawnIO is signed and far safer than WinRing0, but it is still a
  third-party kernel driver maintained by one author; AV vendors have a history of flagging this
  category (see the WinRing0 fiasco above). Mitigation: sensors are strictly optional — the app
  must be fully useful (processes, GPU, RAM, disk) with no driver and no admin.
- **Elevated WinUI 3 edge cases:** elevated packaged apps are Windows 11-only
  ([#896](https://github.com/microsoft/WindowsAppSDK/issues/896)); unpackaged elevation works but
  is the less-tested path — keep an elevated smoke test in CI.
- **VMs / unsupported boards:** SuperIO, MSR/RAPL and SMBus sensors don't exist in VMs and vary
  wildly across motherboards; LHM returns empty sensor trees. UI must render "no sensors" states
  first-class (this is also our CI situation on windows-latest runners, which are VMs).
- **ARM64 (Snapdragon X):** WinAppSDK supports ARM64, but RAPL MSRs, SuperIO chips and the PawnIO
  x86 module ecosystem don't apply — expect processes/GPU/RAM only on ARM64. Ship ARM64 build,
  document reduced sensor coverage.
- **GPU Engine counter cost/availability:** wildcard expansion over hundreds of engine instances
  each second is the most expensive sampler we run — budget it (reuse the query, consider 2 s
  cadence for GPU); counters are absent on older WDDM drivers and headless systems.
- **.NET 8 EOS 2026-11-10** ([source](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/)):
  schedule the net10.0 TFM bump this autumn.
- **NuGet lib vs app divergence:** LibreHardwareMonitorLib NuGet releases (0.9.6, Feb 2026) lag
  master; PawnIO module updates may require lib updates for new CPU generations.
