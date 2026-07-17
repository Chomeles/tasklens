# Taskmanager2 → echtes Produkt („der bessere Taskmanager")

Auftrag (17.07.2026): Taskmanager2 ist nicht mehr nur Satire — es soll der Taskmanager werden, der
allen hilft, die sich sonst Extra-Software installieren, um Dinge unter Windows zu sehen
(Process Explorer, TCPView, HWiNFO, Autoruns). Die 1:1-Win11-Optik bleibt harte Vorgabe
([[taskmanager2-1zu1-vorgabe]]); neue Funktionen nutzen ausschließlich native Win11-Mechanik
(optionale Spalten, Kontextmenüs, ContentDialogs) und deutsche Labels.

Basis: Branch `tm2-real` auf `tm2-fidelity-prozesse` (Stacked PR). Windows-CI validiert Interop;
Linux testet Core-Logik. Bekannte CI-Fallen: neue App-Datei → Link-Compile-Liste in
Taskmanager2.App.csproj UND SmokeTests.csproj; neuer Core-Typ in App.xaml.cs → using; x:Bind ohne
verschachtelte Aufrufe; TextBlock ohne Background (Border).

## Tasks

- [ ] **tm2r-01: Per-Prozess-Netzwerk via ETW** — die eine bewusste Lücke (README, Fidelity-Plan §6).
  ETW-Echtzeit-Session auf `Microsoft-Windows-Kernel-Network` ({7DD42A49-5329-4832-8DFD-43D979153A88}),
  TCP 10/11 (v4) + 26/27 (v6), UDP 42/43 (v4) + 58/59 (v6), Payload uint32 PID + uint32 Bytes.
  Core: `IProcessNetworkService` + reiner Raten-Aggregator (Muster: IGpuProcessService/
  GpuEngineAggregator, Linux-Unit-Tests). App: `EtwProcessNetworkService` + Advapi32-ETW-Interop,
  Session-Waisen beim Start stoppen, ohne Admin → Availability „RequiresAdmin", Zellen ehrlich 0.
  Prozesse-Spalte + Heatmap echt statt hart „0 MBit/s". Smoke-Test: Loopback-Transfer, eigene PID
  taucht auf (CI-Runner sind admin; ohne Rechte → skip).
- [ ] **tm2r-02: Befehlszeile-Spalte (Details)** — echte TM-Spalte, Process-Explorer-Standard.
  `NtQueryInformationProcess` Klasse 60 (ProcessCommandLineInformation), PID-Cache mit Prune wie
  Benutzername/Architektur. Smoke-Test: eigene Befehlszeile enthält Prozessnamen.
- [ ] **tm2r-03: Netzwerkverbindungen-Dialog (TCPView-Ersatz)** — Kontextmenü Details/Prozesse →
  „Netzwerkverbindungen": ContentDialog mit Protokoll / lokale Adresse / Remoteadresse / Status pro
  Prozess. `GetExtendedTcpTable`/`GetExtendedUdpTable` (v4+v6, ohne Admin). Smoke-Test: eigener
  Listener sichtbar.
- [ ] **tm2r-04: Autostart aus Startup-Ordnern (Autoruns-lite)** — User- + Common-Startup-Ordner
  als zweite Quelle neben Registry, .lnk-Ziel via IShellLinkW, Publisher via FileVersionInfo,
  Aktivieren/Deaktivieren über `StartupApproved\StartupFolder`.
- [ ] **tm2r-05: README/Positionierung** — Taskmanager2 als das eigentliche Produkt: „der Taskmanager,
  den du kennst — plus alles, wofür du sonst drei Tools installierst".

## Bewusst offen
- Geplante Aufgaben (COM ITaskService) als Autostart-Quelle — schwerer Blind-COM-Brocken, eigener Task.
- Startauswirkung messen — echter TM nutzt Boot-Traces, unrealistisch; bleibt „Nicht gemessen".
- TaskLens.App-Konsum der neuen Dienste — nach Nutzer-Entscheid, welche App die Haupt-App wird.

Abnahme: Windows-CI grün + Nutzer-Sichtprüfung auf Windows (Checkliste Artefakt d6f180f2).
