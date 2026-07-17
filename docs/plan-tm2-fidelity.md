# Taskmanager2 — Fidelity-Pass (1:1-Imitation, harte Nutzer-Vorgabe)

Ziel: nahezu perfekte optische 1:1-Imitation des Windows-11-Taskmanagers. „Layout-Skizze" reicht
NICHT — visuelle Treue schlägt Diff-Kürze. Abnahme ausschließlich per Sichtvergleich des Nutzers
auf echtem Windows (Screenshot echter TM neben Taskmanager2).

## Erledigt (Branch tm2-fidelity-prozesse)
- [x] Apps/Hintergrundprozesse/Windows-Prozesse-Gruppierung über echte Fenster-Erkennung (User32)
- [x] Heatmap-Zelltönung (gelb→orange) auf CPU, Arbeitsspeicher (%-von-RAM), Datenträger (50-MB/s-Skala, Kalibrier-Knopf in HeatMap.cs), Netzwerk (0)
- [x] Authentische Spaltenköpfe: großer Aggregat-Prozentwert über kleinem Label, Kopfzelle mitgetönt, klickbar sortierbar
- [x] Einklappbare Gruppen mit Chevron links, Zustand überlebt Ticks (persistente Sektionen)
- [x] Prozess-Icons (QueryFullProcessImageName + ExtractAssociatedIcon, gecacht)
- [x] Top-Leiste: „Neuen Task ausführen" (Accent), „Task beenden", „Effizienzmodus", „…" — enabled-Optik, No-ops (App bleibt read-only)
- [x] Zellformate wie im Original: „2.1%", Arbeitsspeicher immer in MB, „0 MBit/s"

## Erledigt (2. Runde, nach Rebase auf tm3)
- [x] Merge mit tm3: „Task beenden" echt (Command + Kontextmenü + Fehler-InfoBar), Satire-Spalten (GPU/Temp/Watt/Lüfter/Verlauf) als native Optional-Spalten rechts
- [x] Globale Suche in der Shell-Kopfzeile (wie im echten TM), Seiten haben große Seitentitel (Prozesse/Leistung/App-Verlauf/Autostart-Apps/Benutzer/Details/Dienste)
- [x] Dezimal-Komma: alle Zellformate auf de-DE („2,1%", „64,0 MB", „54,0 °C")

## Offen — nur mit Windows-Sichtprüfung sinnvoll abschließbar
1. **Screenshot-Abgleich** (Nutzer): echter TM vs. Taskmanager2, Abweichungsliste → Feinschliff-Task pro Fund (Abstände, Zeilenhöhe, Fonts, Farben Light/Dark).
2. **Suche in die echte Titelleiste** (ExtendsContentIntoTitleBar + NonClient-Passthrough) — nur mit Windows-Box riskolos.
3. **App-Zeilen mit Kind-Fenstern**: Chevron pro App-Zeile, eingerückte Fenstertitel-Kinder (braucht Fenstertitel-Enumeration, User32 erweitern).
4. **Generisches Icon** für Prozesse ohne extrahierbares Icon (SHGetStockIconInfo SIID_APPLICATION).
5. **Selektion über Gruppen hinweg** vereinheitlichen (aktuell eine Selektion pro Gruppen-ListView).
6. **Netzwerk-Spalte echt** (ETW-basiert) — größerer Task, eigener Plan-Eintrag.
7. **Leistung-Seite Optik**: linke Kachel-Liste mit Mini-Graphen + großer Graph rechts wie im Original.
8. **Effizienzmodus-Blatt-Icon** und Icon+Text-Buttons prüfen (Glyphen nur auf Windows verifizierbar).

Regel aus [[taskmanager2-1zu1-vorgabe]]: XAML nie blind „vereinfachen" — jede sichtbare Abweichung
vom echten TM ist ein Bug, kein Ponytail-Shortcut.
