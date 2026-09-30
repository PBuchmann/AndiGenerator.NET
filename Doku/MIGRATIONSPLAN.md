# AndiGenerator → C#: Zentraler Migrationsplan

> Stand: 27.09.2026 · Status: **E1–E15 entschieden**; offen sind noch die Klasse-3-Einzelentscheidungen (E9), die Signierung (E12), Frage 12 (CSV-Encoding bestätigen) und die Bereitstellung der Testdaten (Frage 13)
> Grundlage: vollständige Analyse aller 101 Dateien des Delphi-Projekts (Version 24.5.1.0). **Seit 27.09.2026 ist 26.7.1.0 die Referenzversion.** Deren Änderungen sind in Abschnitt 1.5 bewertet. Zu jeder Quelldatei gibt es eine eigene Doku unter [`Dateien/`](Dateien/).
> Dieses Dokument sammelt, was für die Migration gebraucht wird, und führt Schritt für Schritt durch die Vorentscheidungen. Entscheidungen werden in Abschnitt 4 mit **Status** gepflegt.

---

## 1. Ausgangslage

### 1.1 Was die Anwendung tut
Der **AndiGenerator** (Andreas Hofmann, GPL v3, Version 24.5.1) erstellt **Spielpläne für Tischtennis-Staffeln**. Eingabe ist eine **click-TT-Exportdatei (XML)** mit Mannschaften, Heimspiel-Wunschterminen, Sperr- und Ausweichterminen, Koppelwünschen, Nachbar-/Vereinsmannschaften, spielfreien Tagen, Pflichtspieltagen, Setzliste usw. Der Benutzer kann diese Daten in Dialogen ändern (gespeichert als Differenz in `<Datei>.modifications`) und die Gewichtung von 21 Kostenarten einstellen. Dann sucht ein **stochastischer Optimierer auf vielen Threads** fortlaufend bessere Pläne. Gute Pläne werden gespeichert, verglichen, gedruckt und als **CSV für den Rückimport in click-TT** exportiert.

### 1.2 Umfang
| Bereich | Units | Zeilen (ca.) | Anteil |
|---|---|---|---|
| Kern: Datenmodell, Kostenfunktion, Füll-Algorithmus (`PlanTypes`) | 1 | 9 500 | 28 % |
| Optimierung/Threads (`PlanOptimizer`, `PlanMainThread`) | 2 | 960 | 3 % |
| Persistenz/Hilfen (`PlanDataObjects`, `PlanUtils`) | 2 | 3 000 | 9 % |
| Plan-Anzeige + Druck (`PlanPanel`, `PrintUtil`) | 2 | 4 300 | 13 % |
| Hauptfenster (`AndiGeneratorMain`) | 1 | 2 000 | 6 % |
| 42 Dialoge/Panels (`Dialog*`, `EditMandatoryDatesDialog`) | 42 | 14 200 | 41 % |
| Projekt-/IDE-Dateien, FastMM4 (Drittbibliothek) | – | – | entfällt |
| **Summe eigener Code** | **50** | **≈ 34 000** | |

### 1.3 Architektur des Originals (Ist)

```
┌────────────────────────── UI-Thread (VCL, Windows) ──────────────────────────┐
│ AndiGeneratorMain ── PlanPanel (Owner-Draw Tabs/Diagramme) ── PrintUtil       │
│      │   Timer 777 ms: Ergebnis holen, speichern, anzeigen                    │
│      ├── DialogForMultiplePanel / DialogForSinglePanel (Host)                  │
│      │       └── 13 DialogPanel* (Seiten) ── DialogPanelMultiTeams (Master/Detail)
│      │               └── EditOne*-Dialoge (modal)                             │
│      └── DialogOptions (Gewichtungen, live an Optimizer)                       │
└───────────────┬───────────────────────────────────────────────────────────────┘
                │ TPlanData (generischer XML-Baum)  →  TPlan (Arbeitsmodell)
┌───────────────▼──────────── TPlanOptimizer (1 Thread, Takt ~1 s) ─────────────┐
│  4 × TPlanMainThread („Inseln", Takt ~0,2 s)   + 1 Spezial-Insel ab 3 Mio.    │
│     └─ je 14 feste + 10 dynamische TPlanCalcThread (Worker, Sleep(0))         │
│          Worker-Schleife: Rücksetzen → Teil neu würfeln → Termine füllen →    │
│          Kosten mit Early-Exit → besser? → unter Lock nach oben melden        │
│  = 96 bis 120 Worker-Threads, fest verdrahtet (unabhängig von CPU-Kernen)     │
└───────────────────────────────────────────────────────────────────────────────┘
```

Wesentliche Eigenschaften (Details in [PlanTypes](Dateien/PlanTypes.md), [PlanMainThread](Dateien/PlanMainThread.md), [PlanOptimizer](Dateien/PlanOptimizer.md)):
- **Algorithmus:** Ruin-&-Recreate-Local-Search mit strenger Verbesserung (kein Simulated Annealing). Strategien: `R` (Round-Robin-Raster neu), `100/15/5/2/1` (% der Spiele neu), `S<n>,<p>` (n Spieltage), `M<n>,<p>` (n Mannschaften). Dynamische Threads übernehmen die jeweils erfolgreichste Strategie.
- **Inselmodell:** Jede Insel optimiert unabhängig. Die schlechteste Insel wird nach 100 000 Durchläufen ohne Verbesserung neu gestartet (leer oder mit der Lösung der Spezial-Insel, die eine zufällige Kostenart extrem hoch gewichtet).
- **Kostenfunktion:** 16 Mannschafts- und 5 Plan-Kostenarten plus harte Fehler (10¹⁰ je Spiel). Gewichtsstufen 0/1/10/100/10³/10⁴/10⁵ auf drei Ebenen (global × Mannschaft × Mannschaft-und-Kostenart). Feste Reihenfolge „billig → teuer" mit Abbruch, sobald die aktuelle Bestmarke überschritten ist.
- **Inkrementelle Kosten:** Kosten-Cache pro Mannschaft mit Commit/Rollback (`KostenCache`/`KostenCacheRef`). Nur Mannschaften mit geänderten Spielen werden neu berechnet. **Das ist der wichtigste Performance-Mechanismus.**
- **Speicher:** FastMM4 als Multithread-Speichermanager; der Hot-Path erzeugt trotzdem pro Durchlauf temporäre Listen.
- **Leistung laut Autor (2015):** bis 40 000 Pläne/s (2. Kreisliga).

### 1.4 Plattformabhängigkeiten, die ersetzt werden müssen
| Original (Windows/VCL) | Wo | Ersatz in C# |
|---|---|---|
| VCL-Formulare, `TStringGrid`, `TValueListEditor`, `TListView`, `TTreeView`, `TDateTimePicker` | alle Dialoge | Cross-Platform-UI (Abschnitt 4, E2) |
| GDI-Canvas, `TPaintBox`, `SetViewportOrgEx`, `Font.Orientation` | PlanPanel, PrintUtil | SkiaSharp-Rendering |
| VCL-`Printer`, `TPrintDialog`, `GetDeviceCaps` | PrintUtil, DialogPrintSelect | PDF-Export (+ Systemdruck) |
| Registry `HKCU\Software\Andreas Hofmann\Andi-Generator` | Main, DialogGetUpdates | JSON-Einstellungsdatei im App-Datenverzeichnis |
| `CSIDL_LOCAL_APPDATA`, `'\'`-Pfade, Groß-/Kleinschreibung egal | Main, DialogSavePlan | `Environment.SpecialFolder` / plattformspezifischer Storage, `Path.Combine`, Namen normalisieren |
| `ShellExecute` (PDF-Anleitung, Download-URL) | Main, DialogGetUpdates | `Launcher`-Service der UI-Bibliothek |
| `URLDownloadToFile` (WinINet), `TIniFile` | DialogGetUpdates | `HttpClient` + einfacher INI-Parser |
| `TThread`, `TCriticalSection`, `Sleep`, `GetTickCount`, `TerminateThread` | Engine, Update | `Thread`, `lock`/`Interlocked`, `CancellationToken`, `Stopwatch` |
| MSXML (`Xml.XMLDoc`) | PlanDataObjects, PlanTypes | `System.Xml.Linq` / `XmlReader` |
| FastMM4 | dpr | entfällt; dafür allokationsfreier Hot-Path |
| Locale-abhängige Datums-/Zahlenformate | überall | `CultureInfo.InvariantCulture` für Dateien, `de-DE` für Anzeige |

---

### 1.5 Neue Version 26.7.1.0 (Stand 27.09.2026)
Der Quellcode liegt unter `Claude\AndiGenerator 26.7.1.0`. Laut Release Notes wurden nur Hänger korrigiert. Ein normalisierter Vergleich (ohne Groß-/Kleinschreibung, Leerzeichen und Kommentare) bestätigt das:
- **Kostenfunktion, Füll-Algorithmus, Suche: unverändert.** Die ca. 2.200 geänderten Zeilen in `PlanTypes.pas` stammen fast vollständig von einem Code-Formatierer. Übrig bleiben 10 Stellen, alle `try … finally` um `ParentCriticalSection`. **Die Referenzwerte (Referenz/werte) bleiben gültig.**
- `PlanOptimizer`/`PlanMainThread`: `try … finally` um alle Sperren, Ausnahmen werden protokolliert (`LogException` in `PlanUtils`), und `ReInit` + `SetMaxOption` sind zu `ReInitWithMaxOption` zusammengefasst (atomar unter Lock). Damit ist **Befund #11 im Original behoben**.
- `AndiGeneratorMain`: Debug-Ausgabe `SaveToXML('D:\temp\andigenerator.xml')` entfernt. `DialogProfile`: ebenfalls nur `try … finally`.
- Build: neuere Delphi-Version (ProjectVersion 19.2 → 20.1). FastMM4 wird weiter eingebunden (`.dpr`), liegt aber nicht mehr im Projektordner. Neu im Ordner ist das Handbuch als `Andigenerator.docx`.
- Konsequenz für die Migration: keine. Die Einzeldokus unter `Dateien/` beschreiben weiterhin die Logik korrekt. Die Hänger-Ursache (nicht freigegebene Sperren bei Ausnahmen) ist in C# durch `lock`/`using` und das lockfreie Design aus E6 ohnehin ausgeschlossen.

---

## 2. Nebenbedingungen (vorgegeben)

| # | Anforderung | Konsequenz |
|---|---|---|
| N1 | **Client-App** auf dem Gerät, keine Web-App | Native .NET-App je Plattform; keine Browser-/WASM-Auslieferung als Ziel |
| N2 | **Komplett in C#**, kein JavaScript oder andere Sprachen | UI-Bibliothek muss reine C#-UI erlauben (XAML optional, siehe E3); keine Blazor-Hybrid-/Electron-Lösung; keine nativen C/C++-Eigenentwicklungen (Drittbibliotheken wie Skia sind in Ordnung) |
| N3 | Möglichst **alle gebräuchlichen Geräte und Betriebssysteme** | Windows, macOS, Linux; Android und iOS/iPadOS wünschenswert (siehe E1) |
| N4 | Performance **nahe an nativem Code** – präzisiert 27.09.2026: *gleiche Leistung ist nicht wörtlich gefordert* (spätere, bessere Optimierungsverfahren sind ausdrücklich möglich), aber **keine Technik mit deutlich schlechterer Grundleistung** | .NET 10 (LTS) mit JIT + Dynamic PGO bzw. Native AOT; datenorientiertes, allokationsfreies Engine-Design. Abnahme: Pläne/s pro Kern in derselben Größenordnung wie das Original (Richtwert ≥ 80 %); Messung siehe [Referenz/performance.md](../Referenz/performance.md) |
| N5 | **Massive Parallelität** wie im Original | Worker-Anzahl skaliert mit den Kernen; kein Lock und keine Allokation im inneren Loop |
| N6 (abgeleitet) | **Funktionsgleichheit** | Gleiche Kostenfunktion, gleiche Dateiformate (click-TT-Import, CSV-Export), vergleichbare Planqualität; Nachweis durch Tests gegen Referenzdaten |
| N7 (abgeleitet) | **Lizenz GPL v3** | Die C#-Portierung ist ein abgeleitetes Werk und muss bei Weitergabe unter GPL v3 stehen; alle Bibliotheken müssen GPL-kompatibel sein (siehe E11) |

---

## 3. Vorgehen zur Entscheidungsfindung

Die Entscheidungen bauen aufeinander auf. Vorschlag für die Reihenfolge:

```
E1 Zielplattformen ─► E2 UI-Framework ─► E3 XAML oder reines C#
        │                    │
        ▼                    ▼
E4 Runtime/Kompilierung   E5 Schichtenarchitektur ─► E6 Engine-Parallelisierung ─► E7 Datenmodell der Engine
                                   │
                                   ├─► E8 Dateiformate/Kompatibilität
                                   ├─► E9 Umgang mit Original-Bugs
                                   ├─► E10 Druck/Export
                                   └─► E11 Lizenz/Bibliotheken ─► E12 Verteilung/Updates ─► E13 Sprache/Benennung
```

Jede Entscheidung hat: **Frage · Optionen · Bewertung · Empfehlung · Status**. Status-Werte: `offen` → `vorgeschlagen` → `entschieden (Datum)`.

---

## 4. Entscheidungen

### E1 – Zielplattformen und Gerätetypen · Status: **entschieden (27.09.2026)**
**Frage:** Auf welchen Plattformen muss die App laufen, und mit welcher Priorität?

| Option | Bewertung |
|---|---|
| A: Nur Desktop (Windows, macOS, Linux) | Deckt die typische Nutzung ab (Staffelleiter am PC, große Tabellen, lange Optimierungsläufe). Geringstes Risiko. |
| B: Desktop + Tablet (iPad, Android-Tablet) | Große Bildschirme vorhanden; Optimierung läuft, solange die App im Vordergrund ist. Mittlerer Mehraufwand (Touch-Bedienung, Dateizugriff über Picker). |
| C: Zusätzlich Smartphones | Kosten- und Plan-Tabellen sind sehr breit; braucht eigene kompakte Ansichten. Optimierung im Hintergrund wird von iOS/Android gedrosselt oder beendet. Hoher UX-Aufwand bei geringem Nutzen. |

**Zu beachten für Mobile:** iOS erlaubt keinen JIT → Pflicht zu AOT-Kompilierung. Das Betriebssystem kann Apps im Hintergrund pausieren; lange Läufe müssen darum jederzeit unterbrochen und fortgesetzt werden können (Zwischenstand speichern). Dateien können nicht „neben" der click-TT-Datei abgelegt werden (Sandbox), deshalb braucht es eine eigene Projekt-/Arbeitsmappen-Ablage statt `<Datei>.modifications` im selben Ordner.

**Empfehlung:** **Desktop zuerst (Option A) als Release 1**, aber die Architektur von Anfang an so bauen, dass B (Tablet) ohne Umbau folgen kann. Smartphone nur als „Betrachter" (Plan ansehen, Optimierung anstoßen), falls überhaupt.

**Entscheidung (27.09.2026):** Release 1 = **Desktop (Windows, macOS, Linux)**. Die Architektur bleibt tablettauglich: Engine AOT-fähig, Dateizugriff über eine Storage-Abstraktion statt fester Pfade, Optimierungsläufe unterbrechbar und fortsetzbar, keine Desktop-Annahmen in den ViewModels. Smartphones sind nicht geplant.

---

### E2 – UI-Framework · Status: **entschieden (27.09.2026)**
**Frage:** Mit welcher C#-UI-Bibliothek wird die Oberfläche gebaut?

| Kriterium | **Avalonia 12** | **Uno Platform 6** | **.NET MAUI (.NET 10)** | WPF / WinUI 3 |
|---|---|---|---|---|
| Windows / macOS / Linux | ✔ / ✔ / ✔ | ✔ / ✔ / ✔ (Skia) | ✔ / ✔ (Mac Catalyst) / **✘ kein offizielles Linux** | nur Windows |
| Android / iOS | ✔ / ✔ | ✔ / ✔ | ✔ / ✔ | ✘ |
| Rendering | eigenes, Skia-basiert → pixelgleich auf allen Plattformen | Skia (neu Standard) oder nativ | native Steuerelemente je Plattform | nativ |
| Reine C#-UI ohne XAML möglich | ✔ (Code-UI; zusätzlich Community-Bibliotheken für deklaratives C#) | ✔ (C# Markup) | ✔ (C# Markup Toolkit) | eingeschränkt |
| Eigene Zeichenflächen (Diagramme, Zeitachsen) | sehr gut (`Render`-Override, direkter Skia-Zugriff) | gut (SKCanvasElement) | mittel (GraphicsView) | gut |
| DataGrid für große Tabellen | vorhanden (Avalonia.Controls.DataGrid; TreeDataGrid nur kommerziell) | vorhanden (Community Toolkit) | kein eingebautes DataGrid | vorhanden |
| Desktop-Reife (Menüs, mehrere Fenster, Tastatur) | hoch | hoch | mittel (Mobile-first) | sehr hoch |
| Lizenz | MIT | Apache 2.0 | MIT | MIT |

**Empfehlung:** **Avalonia 12** (aktuell 12.1.x). Gründe: echte Desktop-Stärke inklusive Linux, einheitliches Skia-Rendering (die Diagramme aus `PlanPanel` lassen sich 1:1 auf dieselbe Zeichen-API bringen), reife DataGrids, Mobile möglich, reine C#-UI möglich. **Uno Platform** ist die ernsthafte Alternative mit ähnlichem Profil. MAUI scheidet wegen fehlendem Linux-Support aus, WPF/WinUI wegen N3.

**Entscheidung (27.09.2026):** **Avalonia 12** (Stand 27.09.2026: 12.1.3 auf NuGet). Uno Platform (aktuell 6.7) wird nicht weiter verfolgt. Zeichenflächen (PlanPanel, Druck/PDF) werden direkt mit Skia umgesetzt, Tabellen mit **Avalonia.Controls.DataGrid (MIT)**. **Nicht** verwendet wird TreeDataGrid: Es ist inzwischen Teil des kommerziellen „Avalonia Accelerate“ und passt damit nicht zur GPL (vgl. E11). Baumansichten werden mit `TreeView` bzw. eigenen Controls umgesetzt.

---

### E3 – UI-Beschreibung: XAML oder reines C# · Status: **entschieden (27.09.2026)**
**Frage:** Zählt XAML (eine XML-Beschreibungssprache für Oberflächen) als „andere Sprache" im Sinne von N2?

| Option | Bewertung |
|---|---|
| A: XAML-Views + C#-ViewModels (Standard bei Avalonia) | Beste Werkzeugunterstützung (Previewer, Beispiele, Doku), klare Trennung. XAML ist aber zusätzliche Syntax. |
| B: Reines C# (Views im Code) | Erfüllt N2 wörtlich, alles typsicher und refaktorierbar; weniger Beispiele, kein Designer-Previewer. |
| C: Mischform | XAML nur für Styles/Themes, Views in C# |

**Empfehlung:** Entscheidung liegt bei dir. Wenn N2 auch Oberflächen-Markup ausschließen soll → **B**. Technisch gehen beide Wege mit Avalonia; die Engine und alle ViewModels sind ohnehin reines C#.

**Entscheidung (27.09.2026):** **Option A – AXAML-Views + C#-ViewModels.** XAML gilt als deklaratives Markup und nicht als „andere Sprache“ im Sinne von N2. Leitplanken: keine Logik in Code-Behind außer reiner View-Mechanik, **kompilierte Bindings** (`x:CompileBindings`/`x:DataType`) überall (typsicher und AOT-fähig), eigene Zeichen-Controls (Plan, Diagramme) als C#-Klassen mit `Render`-Override.

---

### E4 – Runtime und Kompilierung · Status: **entschieden (27.09.2026)**
**Frage:** Welche .NET-Version und welche Kompilierungsart?

| Option | Bewertung |
|---|---|
| A: **.NET 10 LTS**, JIT mit Tiered Compilation + Dynamic PGO (Standard) | Für lange laufende, heiße Schleifen oft die **höchste Spitzenleistung**, weil der JIT zur Laufzeit profilgesteuert optimiert und die tatsächliche CPU (AVX2/AVX-512/ARM-NEON) nutzt. Start etwas langsamer. |
| B: ReadyToRun (vorkompiliert + JIT) | Schneller Start, danach wie A. Gute Standardwahl für Desktop-Auslieferung. |
| C: Native AOT | Kleinster Speicher, schnellster Start, keine .NET-Installation nötig; kein Dynamic PGO → Spitzenleistung kann leicht unter A liegen. **Auf iOS Pflicht.** Stellt Anforderungen (keine dynamische Codegenerierung, Reflection eingeschränkt). |

**Empfehlung:** **.NET 10 LTS**. Desktop: **B (ReadyToRun, self-contained)**; Engine und Bibliotheken von Anfang an **AOT-kompatibel** schreiben (Analyzer aktivieren), damit iOS/Native AOT jederzeit möglich ist. A vs. C wird **per Benchmark gegen die Delphi-Referenzwerte** entschieden (Phase 3). Upgrade auf .NET 12 LTS (Nov. 2027) einplanen.

**Entscheidung (27.09.2026):** **.NET 10 LTS, Desktop self-contained mit ReadyToRun** (JIT mit Tiered Compilation + Dynamic PGO). Alle Projekte setzen `IsAotCompatible=true` bzw. aktivieren die Trim-/AOT-Analyzer, Warnungen gelten als Fehler. Native AOT wird in Phase 3 gegengemessen und nur dann zum Standard, wenn es gleich schnell oder schneller ist. Ein Upgrade auf .NET 12 LTS ist eingeplant.

---

### E5 – Schichtenarchitektur der Lösung · Status: **entschieden (27.09.2026)**
**Frage:** Wie wird die Solution aufgeteilt?

Vorschlag (Clean-/Hexagonal-Architektur, MVVM in der UI):

```
AndiGenerator.sln
├─ src/
│  ├─ AndiGenerator.Domain         Fachmodell: Staffel, Mannschaft, Termin, Spiel, Optionen, Gewichtung (reines C#, keine Abhängigkeiten)
│  ├─ AndiGenerator.Engine         Kostenfunktion, Füll-Algorithmus, Raster, Worker/Inseln/Optimizer (hochoptimiert, keine UI, AOT-fähig)
│  ├─ AndiGenerator.Persistence    click-TT-Import, eigenes XML/.modifications (Diff/Merge), Optionen, gespeicherte Pläne, CSV-/XML-Export
│  ├─ AndiGenerator.Rendering      Zeichenmodell für Terminplan, Kosten-Heatmap, Diagramme (SkiaSharp) → Bildschirm, PNG, PDF
│  ├─ AndiGenerator.Application    Anwendungsfälle/Services: Plan öffnen, Optimierung steuern, Pläne verwalten, Einstellungen, Updatecheck
│  ├─ AndiGenerator.Presentation   ViewModels (CommunityToolkit.Mvvm), Dialog-/Seiten-Framework (Ersatz für DialogPanel*)
│  ├─ AndiGenerator.UI             Avalonia-Views, Styles, Plattform-Dienste (Dateiauswahl, Launcher)
│  ├─ AndiGenerator.Desktop        Start-Projekt Windows/macOS/Linux
│  └─ AndiGenerator.Android / .iOS Start-Projekte Mobile (später)
├─ tests/
│  ├─ AndiGenerator.Domain.Tests / Engine.Tests / Persistence.Tests   (xUnit)
│  ├─ AndiGenerator.Parity.Tests   Vergleich mit Delphi-Referenzdaten (gleiche Kosten für gleichen Plan)
│  └─ AndiGenerator.Benchmarks     BenchmarkDotNet (Ersatz für DialogProfile)
└─ tools/
   └─ AndiGenerator.Cli            Kommandozeile: optimieren ohne UI (Tests, Benchmarks, Server-/Batchläufe)
```

**Kernaussagen:**
- Die **Engine kennt keine UI** und keinen Dateizugriff. Die UI kennt die Engine nur über `IOptimizationService` (Start, Pause, Stop, Optionen ändern, Snapshot des besten Plans als Ereignis/Observable).
- Das **Dialog-Framework** des Originals (`TDialogPanel` mit `DataToForm/FormToData/toDefault/isDefault/CheckData/OnChange`, Host mit Baum-Navigation, Master/Detail pro Mannschaft) wird als generisches ViewModel-Muster abgebildet: `EditorPageViewModel` → `MultiPageEditorViewModel` → `PerTeamEditorPageViewModel<TDetail>`. Die 7 Wrapper-Units entfallen dabei (reine Konfiguration). Details: [DialogPanel](Dateien/DialogPanel.md), [DialogForMultiplePanel](Dateien/DialogForMultiplePanel.md), [DialogPanelMultiTeams](Dateien/DialogPanelMultiTeams.md).
- **Arbeitskopie-Prinzip** bleibt: Dialoge bearbeiten eine tiefe Kopie, erst „OK" übernimmt; der Optimizer wird währenddessen über den Service pausiert.
- Der **generische String-Baum** `TDataObject` wird durch **typisierte Records** ersetzt. Der Baum bleibt nur noch intern im Persistence-Projekt für Diff/Merge-Kompatibilität.

**Entscheidung (27.09.2026):** Struktur **wie vorgeschlagen** übernommen. Abhängigkeiten nur von außen nach innen (UI → Presentation → Application → Engine/Persistence/Rendering → Domain). **Anpassung 29.09.2026 (Phase 7):** Rendering darf Domain und Engine verwenden (Diagramme zeichnen Engine-Auswertungen), Presentation verweist auf Rendering (baut die Druckbausteine); Rendering bleibt frei von Paketen, SkiaSharp (`SKDocument` für PDF) steckt nur in der UI. Das wird per Projektverweis erzwungen und zusätzlich durch einen Architekturtest abgesichert. Das Android-/iOS-Projekt wird erst angelegt, wenn Tablets anstehen (siehe E1).

---

### E6 – Parallelisierung der Engine · Status: **entschieden (27.09.2026)**
**Frage:** Wird die Thread-Hierarchie 1:1 übernommen oder modernisiert?

| Option | Bewertung |
|---|---|
| A: 1:1 (Optimizer → 4 Inseln → je 24 Worker, fest 96–120 Threads) | Verhalten am nächsten am Original. Aber: Überbelegung auf Rechnern mit wenigen Kernen, Unterauslastung auf Rechnern mit vielen Kernen; auf Mobilgeräten ungeeignet. |
| B: **Gleiches Inselmodell, Größen konfigurierbar** (Default: Worker = `Environment.ProcessorCount`, Inseln = max(2, Kerne/6), Strategiemix proportional) | Gleiche Suchlogik, skaliert von 4 bis 128 Kernen, energiesparend auf Laptops/Mobile (Kernzahl begrenzbar). |
| C: Neues Verfahren (z.B. Simulated Annealing, Tabu-Suche) | Möglicherweise bessere Pläne, verletzt aber die Funktionsgleichheit. Nur als spätere, optionale Erweiterung. |

**Technische Umsetzung (für B):**
- **Dedizierte Threads** (`new Thread { IsBackground = true, Priority = BelowNormal }`) statt ThreadPool/Tasks für die Dauer-Worker; Pause/Stopp über `CancellationToken` und `ManualResetEventSlim` statt Sleep-Schleifen.
- **Eigener Zufallsgenerator pro Worker** (z.B. Xoshiro256**, deterministisch aus Seed ableitbar) statt des globalen, nicht thread-sicheren Delphi-`Random` → reproduzierbare Läufe möglich.
- **Kein Lock im inneren Loop:** Ergebnisaustausch über atomar ersetzte, unveränderliche Snapshots (`Interlocked.Exchange` auf ein kompaktes Termin-Array); Re-Init-Aufträge als Nachricht (`Volatile`-Feld oder `Channel<T>`).
- **Nur den Terminvektor kopieren** (wenige KB) statt ganzer Plan-Objekte (im Original ca. 25 volle `TPlan`-Kopien pro Verbesserung unter Lock).
- **False Sharing vermeiden:** Zähler pro Worker in eigenen, gepolsterten Strukturen.
- Die bekannten Bugs der Steuerlogik (siehe Abschnitt 6) werden bewusst behandelt (E9).

**Empfehlung:** **B**, mit einem Kompatibilitätsprofil „Original" (4 × 24, gleiche Strategien) für Vergleichsmessungen.

**Entscheidung (27.09.2026):** **Option B – Inselmodell skalierend**, zusätzlich das Profil „Original“ (4 × 24) für Vergleichsmessungen.
- **Standard-Kernzahl:** `ProcessorCount − 1` (mindestens 1), in den Einstellungen änderbar (Frage 14).
- **Reproduzierbarkeit:** optionaler Seed. Mit Seed im Einzelthread-/CLI-Modus deterministisch; im Parallelbetrieb bleibt das Ergebnis timingabhängig (Frage 6).

---

### E7 – Datenmodell der Engine (Performance) · Status: **entschieden (27.09.2026)**
**Frage:** Objektorientierte 1:1-Portierung oder datenorientiertes Design für die Engine?

| Option | Bewertung |
|---|---|
| A: 1:1-Klassenportierung (`TPlan`, `TMannschaft`, `TGame` als Klassen mit Listen und Referenzen) | Schnell zu schreiben, leicht mit dem Original zu vergleichen. In .NET aber GC-Druck und schlechtere Cache-Lokalität; Delphi-Niveau vermutlich knapp erreichbar, nicht sicher. |
| B: **Zweiteilung: unveränderliche `PlanDefinition` (Stammdaten, von allen Threads gemeinsam gelesen) + veränderliche `PlanSolution` pro Worker aus Arrays** | Datum als `int`-Tagnummer (+ Minuten), Mannschaften/Spiele als Indizes statt Referenzen und String-Schlüssel (`"HeimId|GastId"`), Bitsets für Sperr-/Ausweichtage, Commit/Rollback per `Array.Copy`/`Span`, **keine Allokation im Hot-Path**. Aufwendiger, aber deutlich bessere Parallel-Skalierung. |

**Wichtig in beiden Fällen:** Die Semantik der Kostenfunktion muss exakt erhalten bleiben, inklusive Eigenheiten wie „Spielliste je Mannschaft nach Datum sortiert, undatierte vorne", „Koppel = Nachbarspiel in dieser Liste", Rundungen und Reihenfolge der Kostenarten (Details in [PlanTypes](Dateien/PlanTypes.md)).

**Empfehlung:** **B**, abgesichert durch **Paritätstests**: Für eine Sammlung realer click-TT-Dateien und fest vorgegebener Pläne muss die C#-Kostenfunktion exakt dieselben Teilkosten liefern wie Delphi. Dafür brauchen wir **Referenzdaten aus der Delphi-Version** (siehe Phase 0).

**Entscheidung (27.09.2026):** **Option B – datenorientiert** (`PlanDefinition` + `PlanSolution` pro Worker).
**Parität (Frage 5), angepasst am 27.09.2026:** Referenz ist die Original-Exe 24.5.1.0. Sie zeigt Kosten nur gerundet an, ein Delphi-Build mit exaktem Kostenexport ist nicht vorgesehen. Deshalb gilt für jeden Referenzplan:
- **Exakt gleich:** Anzahl der Verstöße und Soll-/Gesamtzahl (`Fehler /AllCount`) je Mannschaft und Kostenart sowie die Anzahl harter Fehler.
- **Gleich nach Formatierung:** Kostenwerte je Zelle, Zeilen- und Spaltensummen und die Gesamtkosten müssen, mit `MyFormatFloatShort` (de-DE) formatiert, **dieselbe Zeichenkette** ergeben wie in der Exe.
- **Ergänzend:** Die Reihenfolge der Pläne nach Gesamtkosten muss übereinstimmen. Kostenarten, bei denen die Rundung Abweichungen verdecken könnte (Werte ≥ 100 000), werden zusätzlich mit veränderten Gewichtungen geprüft, damit die Werte in einen genau angezeigten Bereich fallen.
Die Referenzwerte werden per Bildschirmsteuerung aus dem Kosten-Tab abgelesen (Druck nach PDF scheitert an Befund #31); Ablauf in `Referenz/README.md`. Ohne grüne Paritätstests ist Phase 2 nicht abgeschlossen.

---

### E8 – Dateiformate und Kompatibilität · Status: **entschieden (27.09.2026)**
| Datei | Richtung | Vorschlag |
|---|---|---|
| click-TT-Export (XML, Wurzel `TT`) | lesen | **Pflicht, 100 % kompatibel** |
| CSV-Export für click-TT-Import | schreiben | **Pflicht, byte-genau** (Spalten, Trennzeichen, Datumsformat, Encoding klären) |
| `<Datei>.modifications` (Diff-XML) | lesen + schreiben | Lesen **Pflicht** (Bestandsdaten der Anwender); Schreiben im selben Format (v1), damit Delphi- und C#-Version parallel nutzbar sind |
| `AndiGenerator.options` (XML, iso-8859-15, Delphi-Enum-Namen als Werte) | lesen + schreiben | Enum-Namen (`coNormal`, `mktHalleBelegt`, `rpCorona` …) als feste Zeichenketten beibehalten; Encoding beim Schreiben: bleibt iso-8859-15 oder UTF-8 (klären) |
| Gespeicherte Pläne `*.xml` in `%LOCALAPPDATA%\Andi-Generator\<Liga Jahr>\` | lesen + schreiben | kompatibel; plattformabhängiges Basisverzeichnis; Dateinamenkodierung (`#` + 3-stelliger Code) beibehalten |
| Manueller Plan (`<andigenerator><plan>`) | lesen + schreiben | kompatibel |
| Registry-Wert `lastSearch` | – | ersetzt durch Einstellungsdatei; einmalige Übernahme unter Windows optional |

**Empfehlung:** Volle **Lese-Kompatibilität** mit allen Formaten; **Schreiben im Originalformat** in Release 1, damit Anwender gefahrlos wechseln können. Ein neues Projektformat (z.B. eine Datei statt `.modifications` neben der Quelle, nötig für Mobile) erst später und zusätzlich.

**Entscheidung (27.09.2026):** **Lesen und Schreiben aller Originalformate** in Release 1 (Frage 11), damit Delphi- und C#-Version parallel nutzbar sind. Die Optionsdatei wird weiterhin in **iso-8859-15** geschrieben; beim Lesen werden iso-8859-15 und UTF-8 akzeptiert. CSV-Export: wie Delphi **Windows-1252 ohne BOM** (`TStringList.SaveToFile` ohne Encoding). Das muss noch mit einem echten click-TT-Import bestätigt werden (Frage 12).

---

### E9 – Umgang mit Fehlern und Eigenheiten des Originals · Status: **entschieden (27.09.2026)**
Bei der Analyse wurden ca. 50 Auffälligkeiten gefunden (Liste in Abschnitt 6). Sie lassen sich drei Klassen zuordnen:

| Klasse | Beispiele | Vorschlag |
|---|---|---|
| **1 – Abstürze/Speicherfehler** | Array-Überlauf bei > 31 Mannschaften, Division durch 0, Zugriff auf `nil`-Default-Daten, Race `ReInit`→`SetMaxOption` | **Sofort beheben** (in C# ohnehin Exceptions) |
| **2 – UI-/Bedienfehler** | ID-Eindeutigkeitsprüfung vergleicht Namen, Umbenennung zieht Setzliste nicht nach, X-Schließen = „Ja", `isDefault` schreibt Daten | **Beheben**, im Änderungsprotokoll dokumentieren |
| **3 – Verhaltensändernd in Kostenfunktion/Suche** | `assignedGame`-Rückzeiger (Z. 8137), Überlappung letzter Spieltag überschrieben statt summiert (Z. 3944), `DynamicThreads[0]` statt `[i]`, Zähler-Reset der Spezial-Insel wirkungslos, fehlender Spieltag bei ungerader Teamzahl im Raster | **Zuerst 1:1 nachbauen** (Paritätstests grün), dann einzeln per Schalter korrigieren und die Planqualität vergleichen |

**Empfehlung:** Vorgehen wie in der Tabelle. Für Klasse 3 jeweils eine Einzelentscheidung mit dir.

**Entscheidung (27.09.2026):** Vorgehen **nach Klassen** wie in der Tabelle. Klasse-3-Befunde (#3, #4, #5, #6, #9, #10) werden jeweils einzeln entschieden, sobald die Paritätstests grün sind; jeder bekommt einen Schalter `Compat.<Name>` (Standard: Original-Verhalten).
- **Limits (Frage 7):** Das Limit von 30 Mannschaften und die Beschränkung auf Spiellokal 1–5 werden **aufgehoben** (dynamische Größen); Befund #1 ist damit erledigt.
- **Corona-Rundenplanung (Frage 8):** `rpCorona` bleibt **vollständig erhalten**, inklusive Sonderabfrage und UI-Option. Aus Befund #29 wird damit nur der Corona-Teil migriert.

---

### E10 – Druck und Export · Status: **entschieden (27.09.2026)**
Das Original druckt direkt über den Windows-Drucker (7 Abschnitte, Hoch-/Querformat, fester Zoom, nur vertikaler Seitenumbruch, Inhalte werden rechts abgeschnitten).

| Option | Bewertung |
|---|---|
| A: **PDF-Export** mit SkiaSharp (`SKDocument`), dieselbe Zeichenroutine wie für den Bildschirm; danach Öffnen im System-Viewer zum Drucken | Plattformunabhängig, auch auf Mobile; bessere Qualität (Vektor); Seitenumbruch sauber lösbar. |
| B: Zusätzlich direkter Druck je Plattform | Viel Plattformcode, geringer Mehrwert. |
| C: Zusätzlich Excel-/CSV-Export der Pläne | Nützlich für Staffelleiter, geringer Aufwand. |

**Empfehlung:** **A (+ C optional)**. Kopfzeile mit Planname, Datum, Seitenzahl ergänzen.

**Entscheidung (27.09.2026):** **A + C.** Der PDF-Export (7 Abschnitte, sauberer Seitenumbruch horizontal und vertikal, Kopfzeile mit Planname/Datum/Seitenzahl) läuft über `SKDocument` mit derselben Zeichenroutine wie am Bildschirm; gedruckt wird über den System-Viewer. Dazu kommt ein Tabellenexport des Plans als CSV (UTF-8 mit BOM, Excel-tauglich) und als `.xlsx`. Für `.xlsx` ist eine MIT-lizenzierte Bibliothek vorgesehen (z.B. ClosedXML), vorbehaltlich E11. Direkter Plattformdruck ist nicht geplant.

---

### E11 – Lizenz und Bibliotheken · Status: **entschieden (27.09.2026)**
- Das Original steht unter **GPL v3**. Eine Portierung ist ein abgeleitetes Werk → **GPL v3** (sofern sie weitergegeben wird). Rücksprache mit dem Autor (Andreas Hofmann) ist empfehlenswert, auch wegen der Update-Adresse und des Namens.
- Vorgeschlagene Bibliotheken, alle GPL-kompatibel: Avalonia (MIT), SkiaSharp (MIT), CommunityToolkit.Mvvm (MIT), xUnit (Apache 2.0), BenchmarkDotNet (MIT).
- **Nicht** vorgesehen: Bibliotheken mit eigenen kommerziellen oder „Community"-Lizenzen (z.B. QuestPDF, Avalonia TreeDataGrid/Accelerate), weil die GPL-Kompatibilität unklar ist.

**Frage an dich:** Ist die neue App für die Weitergabe (Vereine/Verbände) gedacht oder für den eigenen Gebrauch? Gibt es Kontakt zum Autor?

**Entscheidung (27.09.2026):** **Öffentliche Weitergabe** an Vereine und Verbände, daher **GPL v3** für die gesamte Solution: öffentliches Quellcode-Repository, Lizenztext und Copyright-Hinweis auf das Original (Andreas Hofmann) in der App (Über-Dialog) und im Repository. **Kontakt zum Autor besteht bereits**; Name, Update-Adresse und Rollenverteilung werden mit ihm abgestimmt. Zulässig sind nur Bibliotheken mit MIT-, Apache-2.0-, BSD- oder LGPL-Lizenz; jede neue Abhängigkeit wird in einer `THIRD-PARTY-NOTICES`-Datei geführt.

---

### E12 – Verteilung und Updates · Status: **entschieden (27.09.2026)**
| Plattform | Vorschlag |
|---|---|
| Windows | self-contained, signiertes Installationspaket (MSIX oder Setup) bzw. ZIP; Updates über ein Update-Framework (z.B. Velopack) oder den bestehenden Mechanismus (INI mit Version + URL) |
| macOS | signiertes und notarisiertes `.app` im `.dmg` (Apple-Entwicklerkonto nötig) |
| Linux | AppImage und/oder Flatpak |
| Android/iOS | Stores (Entwicklerkonten nötig) |

Der Updatecheck des Originals (`https://sv-schwaig.de/download/generator/updateinfo.ini`, höchstens alle 2 Tage) wird durch einen asynchronen `HttpClient`-Check mit Timeout ersetzt; die URL aus dem Internet wird vor dem Öffnen geprüft.

**Frage an dich:** Wer hostet künftig Downloads und Update-Info?

**Entscheidung (27.09.2026):**
- **Hosting:** **GitHub Releases** im öffentlichen Repository, Builds per CI (GitHub Actions) für win-x64, osx-arm64/osx-x64 und linux-x64. sv-schwaig.de verlinkt darauf (mit dem Autor abstimmen).
- **Updates:** **Velopack** (MIT) mit GitHub Releases als Quelle, Update auf Knopfdruck. Der alte INI-Mechanismus entfällt.
- **Signierung:** Die Beta wird **zunächst unsigniert** ausgeliefert. Ob signiert wird (Windows-Zertifikat, Apple-Notarisierung), entscheiden wir **vor der öffentlichen Freigabe** (offener Punkt für Phase 8/9).

---

### E13 – Sprache, Benennung, Lokalisierung · Status: **entschieden (27.09.2026)**
- **Oberfläche:** Deutsch (wie Original). Texte trotzdem in Ressourcendateien, damit spätere Übersetzungen möglich sind?
- **Code-Bezeichner:** Das Original mischt Deutsch/Englisch (`Mannschaft`, `Kosten`, `Wunschtermin`, `game`, `teamId`). Vorschlag: **Englische Bezeichner im Code**, mit einem **Glossar** der Fachbegriffe (Koppeltermin = `HomeCoupleDate`, Auswärtskoppel = `AwayCouple`, Sperrtermin = `BlockedDate`, Ausweichtermin = `AlternativeDate`, Pflichtspieltag = `MandatoryGameDay`, Nachbarmannschaft = `SisterTeam`, Setzliste = `Ranking` …). Die Fachbegriffe in der UI bleiben deutsch.

**Entscheidung (27.09.2026):**
- **Fachbegriffe im Code auf Deutsch** (z.B. `Mannschaft`, `Spiel`, `Koppeltermin`, `Sperrtermin`, `Ausweichtermin`, `Pflichtspieltag`, `Nachbarmannschaft`, `Setzliste`, `Kostenart`), damit Code, Original und Fachsprache übereinstimmen. **Technische Begriffe** und .NET-Muster bleiben englisch (`ViewModel`, `Service`, `Repository`, `Load/Save`, `Engine`, Projektnamen aus E5). Bezeichner werden ohne Umlaute geschrieben (`ae/oe/ue/ss`, z.B. `Auswaertskoppel`, `Gewichtung`), um Encoding-Probleme in Werkzeugen zu vermeiden. Ein kurzes Glossar Original-Bezeichner → C#-Bezeichner wird trotzdem geführt.
- **Oberflächentexte** liegen in `.resx`-Ressourcen; Release 1 gibt es nur auf Deutsch.
- **Entwickler-Werkzeuge (Frage 15):** Profiling-Dialog und Testdatengenerator werden **nicht** in die App übernommen, sondern gehen in `AndiGenerator.Benchmarks` bzw. `AndiGenerator.Cli`.

---

### E14 – Automatische Gewichtungsanpassung (Erweiterung nach Release 1) · Status: **entschieden (27.09.2026)**
**Anlass (Peter):** Wer im Frontend die Gewichtungen nachschärft, bekommt oft deutlich bessere Pläne. Ziel ist dabei nicht eine kleinere Kostensumme, sondern **weniger Kollisionen**, also kleinere Verstoß-Anzahlen (z.B. weniger verletzte Sperrtermine). Die Performance-Messung stützt das: Bei M1 war der beste Plan nach ca. 15 s erreicht, danach gab es 4 Minuten lang keine Verbesserung mehr (lokales Optimum).

**Idee:** Ein Automatismus variiert die Gewichtungen selbst.
1. **Getrennte Zielgröße:** Die Suche optimiert weiter die gewichtete Kostensumme. Die Frage, ob ein Plan „besser“ ist, beantwortet aber ein eigenes Qualitätsmaß aus den **Verstoß-Anzahlen je Kostenart** in konfigurierbarer Rangfolge (lexikografisch, z.B. harte Fehler > Hallenbelegung > Sperrtermine > …).
2. **Adaptive Gewichte** („Guided Local Search“ / adaptive Straffunktionen): Kostenarten mit hartnäckigen Verstößen werden schrittweise höher gewichtet und später zurückgesetzt.
3. **Inseln mit unterschiedlichen Gewichtungsprofilen**, die ihre besten Pläne austauschen. Das verallgemeinert die Spezial-Insel des Originals.

**Einordnung:** Nicht in Release 1 (dort gilt Funktionsgleichheit, E7/E9). Die Architektur muss es aber **ohne Umbau** erlauben:
- Die Engine bekommt die Gewichtung als austauschbares, unveränderliches Objekt **pro Insel** (nicht global). Wechsel zur Laufzeit bleiben möglich (Live-Übernahme, Frage 9).
- Die Kostenberechnung liefert neben der Summe immer auch den **Vektor der Anzahlen je Kostenart** (existiert im Original als `TKostenValue.Anzahl`) und hält ihn im Snapshot des besten Plans vor.
- Das Vergleichskriterium „besserer Plan“ ist eine austauschbare Strategie (`IPlanComparer`): Standard = Kostensumme (Original), später lexikografisch nach Anzahlen.
- Die Insel-Steuerung (Neustart, Austausch, Spezial-Insel) wird als austauschbare Strategie gekapselt (`IIslandPolicy`).
- Das CLI-Werkzeug (E5) kann Gewichtungsprofile über mehrere Läufe vergleichen und bildet damit die Grundlage für Experimente.

**Rangfolge „besserer Plan“ (Vorgabe Peter, 27.09.2026)**, lexikografisch ausgewertet: Eine Stufe zählt erst, wenn alle höheren gleich sind.

| Stufe | Kriterium | Kostenart(en) im Original | Ziel |
|---|---|---|---|
| **A1** | Alle Begegnungen terminiert | ungültige Spiele / harte Fehler (undatierte Spiele, ungültige Termine) | **0 – Pflicht** |
| **A2** | Hallenbelegung | Hallenbelegung | **0, wann immer möglich** |
| **A3** | Parallele Spiele | parallele Spiele | so wenig wie möglich |
| **A4** | Paarungen von Mannschaften desselben Vereins am Anfang | Vereinsinterne Spiele am Anfang (Plan-Kostenart) | **einhalten** |
| **A5** | Letzter Pflichtspieltag wird von allen Mannschaften wahrgenommen (damit nicht einige Mannschaften schon fertig sind, während andere noch spielen) | Pflichtspieltage (`mktMandatoryGames`), insbesondere der letzte Pflichtzeitraum der Runde | **einhalten**; Ausnahme bei ungerader Mannschaftszahl, siehe unten |
| B1 | Sperrtermine | Sperrtermine | möglichst 0, aber **Ermessenssache**: Bei sehr vielen gemeldeten Sperrterminen (z.B. 100+) ist ein Rest vertretbar. Vorschlag: Bewertung relativ zur Zahl der gemeldeten Sperrtermine (das Original zeigt schon „Verstöße /gemeldet“, z.B. `5 /178`) |
| B2 | Ausweichtermine | Ausweichtermine | so wenig wie möglich |
| C | Alle übrigen Kostenarten (3-Tage-Abstand, 2 Spiele pro Woche, Spielverteilung, H/A-Wechsel/-Abstand/-Ungleichheit, Koppel, 60 km, Pflichtspieltage, Überlappung/Länge Spieltage …) | – | „nice to have“, je kleiner desto besser (hier genügt die gewichtete Summe) |

**Pflichtspieltag bei ungerader Mannschaftszahl (Vorgabe Peter):** Am letzten Pflichtspieltag spielen alle Mannschaften. Bei ungerader Zahl ist eine Mannschaft zwangsläufig spielfrei. Diese Mannschaft soll dann **möglichst am vorletzten Spieltag** spielen.
- *Verhalten des Originals:* Die Kostenart zählt nur die fehlenden Spiele je Mannschaft im Pflichtzeitraum (`fehlende² · 1000 · Faktor`). Bei ungerader Zahl wird die spielfreie Mannschaft also **immer** bestraft, obwohl es unvermeidbar ist. Beispiel: Referenzfall R3 (9 Mannschaften), Tlpg Vsxc „1 Spiel(e) zu wenig vom Mo 12.04.2027 – So 18.04.2027“ = 100 T. Das verzerrt den Kostenvergleich, weil ein fixer Sockelbetrag die eigentlich interessanten Verstöße überlagert. Einen Hinweis „vorletzter Spieltag“ gibt es nicht. Der Erststart-Assistent empfiehlt nur, den Zeitraum von Hand zu verlängern.
- *Vorschlag für die Erweiterung:* Die Kostenart Pflichtspieltage erkennt den unvermeidbaren Fall selbst: Bei ungerader Mannschaftszahl und einem Pflichtzeitraum mit 1 Spiel ist genau eine fehlende Mannschaft zulässig, wenn diese im vorangehenden Spieltag (Woche davor) gespielt hat. Sonst wird wie bisher bestraft. In Release 1 bleibt das Originalverhalten (Parität, E7/E9). Die neue Regel kommt als Schalter wie die Klasse-3-Korrekturen.

**Folgerungen für den Automatismus:**
- Stufe A ist ein **Muss**. Ein Plan, der A verletzt, ist schlechter als jeder Plan, der A erfüllt, egal wie gut der Rest ist. Die Suche soll erst A erfüllen und dann B und C verbessern, ohne A wieder zu verletzen.
- Die gewichtete Kostensumme bleibt als Steuergröße der Suche erhalten. Die Rangfolge entscheidet, welcher Plan „der beste“ ist und welche Gewichte der Automatismus anhebt: zuerst die der höchsten Stufe mit Verstößen.
- Die Rangfolge wird **konfigurierbar** (Standard = obige Tabelle), weil andere Staffelleiter andere Schwerpunkte setzen könnten.

**Entscheidung (27.09.2026):** Der Automatismus wird eine **vollautomatische Sonderfunktion** (eigener Modus neben der normalen Generierung). Er wird eingebaut, **sobald die Originalfunktion nachweislich hergestellt ist** (Paritätstests grün, Parallelbetrieb abgeschlossen), also als Folgestufe von Release 1 (Phase 10). Die normale Generierung bleibt unverändert und originalgetreu. Die Sonderfunktion variiert die Gewichtungen selbständig nach der Rangfolge A → B → C und liefert den nach dieser Rangfolge besten Plan. ~~Ist mit „letzter Spieltag eingehalten“ die Kostenart „Überlappung letzter Spieltag“ gemeint?~~ → Nein, gemeint ist der letzte Pflichtspieltag (A5).

### E15 – Anordnung der Ansichten im Hauptfenster · Status: **entschieden (28.09.2026)**

Im Original zeigt das Hauptfenster die Ansichten (Abweichungen/Kosten, Plan, Spieltage, …) als Reiter; sichtbar ist immer nur einer. Wunsch (Peter): den laufenden Generierungsvorgang in mehreren Ansichten **gleichzeitig** beobachten.

**Entscheidung (28.09.2026):**
- **Andock-Layout** mit **Dock.Avalonia** (MIT, vgl. E11; Pakete `Dock.Avalonia`, `Dock.Avalonia.Themes.Fluent`, `Dock.Model.Mvvm`): Jede Ansicht ist ein Dokument, das als Reiter, nebeneinander/untereinander angedockt oder als eigenes Fenster (z. B. auf einem zweiten Bildschirm) angezeigt werden kann. Dieselbe Ansichtsart darf mehrfach geöffnet sein.
- **Planquelle je Ansicht:** Jede Ansicht wählt selbst, welchen Plan sie zeigt – die laufende Generierung (aktualisiert sich bei jeder Verbesserung), den in click-TT vorhandenen Plan oder einen gemerkten Plan. So lassen sich z. B. die Kosten des laufenden Plans neben denen eines gemerkten Plans vergleichen.
- Die Ansichtslogik liegt in `AndiGenerator.Presentation` (ViewModels, abhängig von `Dock.Model.Mvvm`, ohne Avalonia-Steuerelemente); Views in `AndiGenerator.UI`, Programmstart in `AndiGenerator.Desktop`.
- Später (nicht im ersten Gerüst): Speichern/Wiederherstellen der Anordnung, Gewichte per Klick in der Kostenansicht ändern (Phase 5).


### E16 – Programmname · Status: **entschieden (29.09.2026)**

Die Portierung soll sich deutlich vom Original unterscheiden, ohne ihre Herkunft zu verbergen. „AndiGenerator V2“ wurde verworfen, weil es wie eine Folgeversion des Originalautors klingt (vgl. GPL-3.0 §7c).

**Entscheidung (29.09.2026, Peter):** **AndiGenerator.NET**.
- Sichtbar: Fenstertitel, Dialog „Über“, Programmdatei `AndiGenerator.NET.exe`, Produktname der Programmdateien, Dateiköpfe im SPDX-Format nach der REUSE-Spezifikation (`SPDX-FileCopyrightText: 2017 Andreas Hofmann`, `SPDX-FileCopyrightText: 2026 Peter Buchmann`, `SPDX-License-Identifier: GPL-3.0-only`; von StyleCop über `stylecop.json` bei jedem Build geprüft; Skripte unter `tools/` sind reine Neuentwicklungen und nennen nur Peter Buchmann).
- Eigener Datenordner `%LOCALAPPDATA%\AndiGenerator.NET` (vorher `AndiGeneratorNeu`); beim ersten Start werden die Staffelordner von dort einmalig übernommen (ohne Überschreiben, der alte Ordner bleibt unverändert). Build-Ausgaben unter `%LOCALAPPDATA%\AndiGenerator.NET\artifacts`.
- **Unverändert** bleiben die Dateiformate des Originals (`AndiGenerator.options`, `<andigenerator>`-XML, `.modifications`; E8) und die internen Projekt- und Namensräume `AndiGenerator.*` (technische Bezeichner, für Anwender unsichtbar; eine Umbenennung brächte nur Aufwand).
- Herkunft: Über-Dialog, `LIZENZ.md`, `Doku/ENTSTEHUNG.md`.
---

## 5. Migrations-Fahrplan (Phasen)

| Phase | Inhalt | Ergebnis / Abnahmekriterium | Grober Aufwand |
|---|---|---|---|
| **0 – Entscheidungen & Referenzdaten** | E1–E13 entscheiden. **5–10 reale click-TT-Dateien** sammeln (kleine/große Staffeln, mit Koppel-/Auswärtskoppel-/Nachbarmannschaften). Aus Delphi exportieren: Pläne (XML), CSV, Kosten je Kostenart und Mannschaft (Screenshot/Druck bzw. kleines Delphi-Hilfsprogramm), Pläne/s-Messung. | Entscheidungsprotokoll, Referenz-Datensatz, Performance-Basislinie | 1–2 Wochen |
| **1 – Domain + Persistence** (abgeschlossen 27.09.2026) | Fachmodell, click-TT-Import, `.modifications` Diff/Merge, Optionen, gespeicherte Pläne, CSV-Export | Round-Trip-Tests: Delphi-Dateien lesen → schreiben → identisch | 2–3 Wochen |
| **2 – Engine einsträngig** | Kostenfunktion (21 Kostenarten + harte Fehler), inkrementeller Cache, Füll-Algorithmus, Raster, Neu-Würfeln-Strategien | **Paritätstests grün**: gleiche Kosten wie Delphi für alle Referenzpläne | 4–6 Wochen (größter Block) |
| **3 – Engine parallel + Tuning** | Inseln/Worker/Optimizer, Pause/Re-Init, CLI, BenchmarkDotNet; JIT vs. AOT messen | **Performance:** Pläne/s pro Kern in derselben Größenordnung wie das Original (Richtwert ≥ 80 %, kurze Läufe von 1–2 Min genügen). **Optimierungsqualität** (getrennt davon): Läufe bis zur Konvergenz je Staffel; erreichte Kosten bzw. Verstöße mindestens gleich gut wie im Original, Zeit bis zur Konvergenz vergleichbar (statistisch über mehrere Läufe, abhängig von der Komplexität der Staffel) | 2–3 Wochen |
| **4 – UI-Grundgerüst** (abgeschlossen 28.09.2026) | Hauptfenster, Öffnen/Neu, Start/Pause, Plan-Auswahl, Optimierungsstatus, Einstellungen (DialogOptions) | Plan laden, optimieren, Ergebnis sehen und exportieren – ohne Datendialoge | 2–3 Wochen |
| **5 – Plan-Anzeige & Diagramme** (abgeschlossen 28.09.2026) | 5 Tabs aus `PlanPanel` (Terminwünsche, Nachbarmannschaften, Kosten-Heatmap mit Klick-Gewichtung, Terminplan, Diagramme) auf Skia | Visuelle Abnahme gegen Original-Screenshots | 3–4 Wochen |
| **6 – Datendialoge** (abgeschlossen 29.09.2026: Seiten-Framework, Speichern als `.modifications`, alle 13 Seiten samt Bearbeitungsdialogen, Erststart-Assistent mit Bearbeiten-Knöpfen; Befunde #16, #21, #22, #23 behoben, #27 teilweise. **Abgleich mit dem Original:** 14 im Original gemachte Bearbeitungen (`Referenz/datendialoge`) ergeben inhaltsgleiche `.modifications`, bei D03 bis auf die gewollte Korrektur #21; 745/745 Tests grün, 0 Warnungen) | Seiten-Framework + 13 Seiten + EditOne-Dialoge + Erststart-Assistent + gespeicherte Pläne | Alle Bearbeitungen erzeugen identische `.modifications` wie Delphi | 4–5 Wochen |
| **7 – Export, Druck, Anleitung** (begonnen 29.09.2026: Druckmodell und Seitenumbruch in `Rendering`, Druckauswahl, PDF über SkiaSharp mit allen 7 Abschnitten; Terminwunschraster und Diagramme zeichnen Bildschirm und Ausdruck mit derselben Routine (`Diagrammzeichner`, `Terminwunschzeichner` in `Rendering`), im Ausdruck jedes Diagramm einzeln und bei Bedarf verkleinert; Dialog „Über“ mit Urhebern und GPL-Hinweisen (`Doku/ENTSTEHUNG.md`); Excel-Export (eigener xlsx-Schreiber ohne Fremdbibliothek: Übersicht, Spielplan und Mannschaftspläne als filterbare Listen mit echten Datums- und Zeitwerten, Kostentabelle mit Hinterlegung); Anleitung `Doku/ANLEITUNG.md` mit 17 Bildschirmfotos und daraus `Doku/Anleitung.pdf` (30 Seiten, `tools/anleitung`), im Programm über „Anleitung“ (30.09.2026). Der Updatecheck wandert nach Phase 8 (Velopack/GitHub Releases)) | PDF-Export (7 Abschnitte), Druckauswahl, Anleitung | – | 1–2 Wochen |
| **8 – Plattformen & Auslieferung** | Pakete für Windows/macOS/Linux, Signierung, ggf. Tablet | Installierbare Builds | 1–2 Wochen |
| **9 – Parallelbetrieb/Beta** | Echte Staffelleiter planen mit beiden Versionen; Klasse-3-Bugfixes einzeln aktivieren | Freigabe | 2–4 Wochen |
| **10 – Sonderfunktion Auto-Gewichtung (E14)** | Qualitätsmaß nach Rangfolge A/B/C, adaptive Gewichte je Insel, Pflichtspieltag-Regel für ungerade Mannschaftszahl (Befund #33, als Schalter) | Auf den Referenz- und Testdaten bei Läufen bis zur Konvergenz im Mittel weniger Verstöße in Stufe A/B als die normale Generierung | 3–5 Wochen |

**Gesamtaufwand grob:** ca. 22–34 Personenwochen für Release 1 (Desktop). Grundlage sind die Einschätzungen in den Einzeldokus: 1 × XL (PlanTypes), 3 × L (AndiGeneratorMain, PlanPanel, Druck), ca. 15 × M, Rest S.

---

## 6. Konsolidierte Liste der Auffälligkeiten im Original
(Details und Zeilennummern in den jeweiligen Einzeldokus; Klasse gemäß E9)

| # | Datei | Befund | Klasse |
|---|---|---|---|
| 1 | PlanTypes Z. 5235/6120 | Hartes Limit 30 Mannschaften (`MAX_MANNSCHAFT`, Exception nur in „Abstand H/A“); Auswärtskoppel-Kosten prüfen nicht und schreiben bei > 31 Mannschaften über das Array hinaus | 1 |
| 2 | PlanTypes Z. 6509 | Division durch 0 in „Spielverteilung", wenn ein Team keine Spiele hat | 1 |
| 3 | PlanTypes Z. 8137 | `setDate` löscht `assignedGame` des Wunschtermins ohne Besitzprüfung → Termin gilt ggf. fälschlich als frei | 3 |
| 4 | PlanTypes Z. 3944 | Überlappungskosten letzter Spieltag überschrieben statt summiert | 3 |
| 5 | PlanTypes Z. 1436 | Bei ungerader Teamzahl fehlt im Raster ein Spieltag | 3 |
| 6 | PlanTypes Z. 4695 | Auswärtskoppel-Pfad kann bereits datierte Spiele neu terminieren | 3 |
| 7 | PlanTypes Z. 8562 | Fehlendes Freitags-Attribut → `false` statt Standard `true`; Ladefehler verschluckt | 2 |
| 8 | PlanTypes (CSV) | Lücken in der Nummerierung; undatierte Spiele mit `DateBegin` exportiert | 2 |
| 9 | PlanMainThread Z. 199 | `DynamicThreads[0]` statt `[i]` → 1/3-Grenze wirkungslos – **in C# korrigiert** (Entscheidung 27.09.2026, `Insel.DynamischeAnpassen`) | 3 |
| 10 | PlanOptimizer Z. 185 | Zähler-Reset der Spezial-Insel wirkungslos → nach erster Schwelle nie mehr Neustart „leer" – **in C# korrigiert** (Entscheidung 27.09.2026, `Insel.SeitNeustart`) | 3 |
| 11 | PlanOptimizer/PlanMainThread | Race `ReInit` → `SetMaxOption` → mögliche Zugriffsverletzung; `ReInitAll` ohne Lock (**in 26.7.1.0 behoben**; dazu Hänger durch nicht freigegebene Sperren bei Ausnahmen) | 1 |
| 12 | PlanTypes | Globales `Random` aus allen Threads; `SetThreadType` ohne Lock | 1 |
| 13 | PlanPanel Z. 1784/1816, 2586, 2599 | Formularbreite versehentlich gesetzt; Größenänderung beim Zeichnen; modaler Dialog während Iteration | 2 |
| 14 | PlanPanel Z. 2931–3038, 1947 | Doppelte Vorrunden-Auswertung; Legende passt nicht zu Farben | 2 |
| 15 | PrintUtil Z. 386, 244 | Y-Umrechnung mit X-Faktor; leere Zusatzseite; kein Clipping; gewählte Ausrichtung überschrieben | 2 |
| 16 | DialogPanelTeams Z. 159/244 | ID-Eindeutigkeitsprüfung vergleicht Namen; Umbenennung zieht Setzliste/Vorgaben nicht nach – **in C# behoben** (28.09.2026: ID wird mit IDs verglichen; Umbenennung zieht Setzliste, Heimrecht, 60 km, Auswärtskoppeln und vorgegebene Spiele nach) | 2 |
| 17 | DialogPanelRanking Z. 110 | Duplikate bei Lücken in `rankingindex`; Drop unter letztem Eintrag → Exception | 1 |
| 18 | DialogPanelHomeDaysDetail Z. 92 | Doppelspieltag-Prüfung testet falsche Variable; Termine nach Tag 360 gehen verloren; ein Termin/Tag | 2 |
| 19 | DialogPanelHomeCoupleCompactDetail | Kombinationen außerhalb der 13er-Liste werden gelöscht; zweite Anfangszeit 00:00 | 2 |
| 20 | DialogPanelAuswaertsKoppelDetail Z. 470/503, HomeCoupleCompactDetail Z. 571, SisterTeamsDetail | Absturz bei fehlenden Default-Daten | 1 |
| 21 | DialogPanelHomeRightDetail Z. 71 | Fehlendes Attribut → „Vorrunde: 0" statt „Automatisch" – **in C# behoben** (29.09.2026: fehlendes Attribut wird als „Automatisch“ angezeigt und bleibt ohne Änderung fehlend; neue Mannschaften erhalten wie beim click-TT-Import `homerights="-1"`; Werte außerhalb der Liste bleiben erhalten) | 2 |
| 22 | DialogPanelFreeDays / LocationsCompactDetail | Freie Tage ohne Wunschtermin gelöscht; doppelte Zeilenschlüssel – **in C# behoben** (28.09.2026: freie Tage bleiben erhalten, eine Zeile je Heimtermin) | 2 |
| 23 | Panel-Framework | `isDefault` ruft `FormToData` auf (Anzeigen verändert Daten); „Standard" uneinheitlich; Single-Host validiert nicht – **in C# behoben** (28.09.2026: `IstStandard` verändert keine Daten, Seitenwechsel nur mit gültigen Eingaben) | 2 |
| 24 | DialogSavePlan Z. 133 | Klick in leeren Listenbereich → Zugriffsverletzung | 1 |
| 25 | DialogGetUpdates | `TerminateThread`; Fehler → „ist aktuell"; Versionsvergleich 1.2.0.1 vs 1.2; URL ungeprüft an ShellExecute | 1/2 |
| 26 | DialogEditSisterTeam Z. 204, DialogEditOneAuswaertsKoppel, DialogEditOneHomeDay | Row-Index außerhalb; leerer 4. Eintrag → `sameday=3`; Altwerte bleiben erhalten | 2 |
| 27 | EditMandatoryDatesDialog, DialogQuestionUseUserOptions, DialogProfile | Exception bei Wert außerhalb 1–10; X = „Ja"; Division durch 0 – Pflichtspieltag **in C# behoben** (28.09.2026: Anzahl auf 1–10 begrenzt), Rest offen | 2 |
| 28 | andigenerator64.manifest | `processorArchitecture="ia64"` statt `amd64` | – |
| 29 | Toter Code | `DialogCreateTestData`, `DialogPanel.dfm`, `DialogPanel60km.dfm` | nicht migrieren; **Corona-Sonderfall wird migriert** (E9) |
| 30 | PlanTypes (CSV) | CSV-Kopfzeile hat 15 Felder, Datenzeilen 16 (abschließendes `;`) – für Byte-Parität beibehalten | – (Eigenheit) |
| 31 | Druck (PrintUtil/Main) | Wechsel des Druckers im Druckdialog (z.B. auf „Microsoft Print to PDF“) → „Ausgewählter Drucker ist ungültig“ / „Operation auf ausgewähltem Drucker nicht verfügbar“; gedruckt werden kann nur auf den Windows-Standarddrucker | 2 (entfällt mit PDF-Export, E10) |
| 32 | PlanDataObjects/PlanTypes (Import) | Mannschaftsnamen im click-TT-Export teils mit führendem Leerzeichen; das Original trimmt beim Laden, CSV und `.modifications` nutzen den getrimmten Namen | – (Eigenheit, beibehalten) |
| 33 | PlanTypes (`mktMandatoryGames`) | Bei ungerader Mannschaftszahl wird die am Pflichtspieltag zwangsläufig spielfreie Mannschaft immer bestraft (z.B. R3: 100 T); keine Regel „spielfreie Mannschaft spielt am vorletzten Spieltag“ | 3 (fachliche Erweiterung, siehe E14/A5) |
| 34 | AndiGeneratorMain (`OpenFile`) | Dialog „Benutzereinstellungen“: **„Nein, die Standardeinstellungen verwenden“ setzt die Optionen zurück und speichert sie sofort**, ohne Warnung und ohne Sicherung. Die gespeicherten Einstellungen der Staffel sind danach verloren (am 27.09.2026 bei den Referenzmessungen für 19 Staffeln passiert). C#: nie stillschweigend überschreiben; „Standard nur für diese Sitzung“ anbieten, vor dem Überschreiben sichern | 2 |
| 35 | AndiGeneratorMain (`GetCurrentPlanDirectory`), AndiGeneratorMain (`ButtonSaveClick`) | Staffelordner = `Name + ' ' + Jahr(Beginn)`: ein Plan ohne Namen/Beginn (z. B. „Neu“) landet im Ordner „ 1899“ (vorhanden). Gemerkte Pläne speichern keine Spiellokale (Namensvergleich im Dialog „Plan merken“: getrimmt, ohne Groß-/Kleinschreibung, mit Rückfrage vor dem Überschreiben). C#: Verhalten beim Lesen/Schreiben übernommen; Ordner ohne Staffelnamen später abfangen (ein führendes Leerzeichen im Namen ist unter Windows problematisch, OneDrive synchronisiert solche Namen z. B. nicht) | 1 |

---

## 7. Gesammelte offene Fragen an dich
**Scope & Produkt**
1. ~~Welche Zielplattformen in Release 1?~~ → Desktop, tablettauglich (E1)
2. ~~Gilt XAML als „andere Sprache“ (E3)?~~ → nein, AXAML erlaubt (E3)
3. ~~Für wen ist die App gedacht? Kontakt zum Autor?~~ → öffentlich (GPL v3), Kontakt besteht (E11)
4. ~~Wer hostet künftig Downloads und Update-Info?~~ → GitHub Releases + Velopack (E12)

**Funktionsgleichheit**
5. ~~Müssen die Kosten **numerisch identisch** zu Delphi sein?~~ → Anzahlen exakt, Kosten gleich nach Anzeige-Formatierung (E7, angepasst)
6. ~~Sollen Optimierungsläufe **reproduzierbar** sein (fester Seed)?~~ → optionaler Seed (E6)
7. ~~Bleibt das Limit von 30 Mannschaften bzw. Spiellokal 1–5?~~ → aufgehoben (E9)
8. ~~Soll die Corona-Rundenplanung erhalten bleiben?~~ → ja, vollständig (E9)
9. ~~Live-Übernahme geänderter Gewichtungen?~~ → ja, live wie im Original
10. ~~Neue Funktionen in Release 1?~~ → nein, erst Parität; Drag&Drop/Plan-Vergleich frühestens ab Release 2

**Daten**
11. ~~Müssen `.modifications`, Optionsdateien und gespeicherte Pläne lesbar und schreibbar bleiben?~~ → ja, beides; Optionen in iso-8859-15 (E8)
12. ~~CSV-Encoding für click-TT?~~ → **Windows-1252, CRLF, ohne BOM wird von click-TT akzeptiert** (belegt: `4. Kreisklasse.csv` kommt 1:1 als bestehender Plan im nächsten Export zurück). Schema des Exports: `TTGenerator.xsd` ist **nicht öffentlich verfügbar** (nur verlinkt). Der Import stützt sich auf die Delphi-Implementierung und die Testdaten.
13. ~~Reale click-TT-Dateien?~~ → **bereitgestellt** (50 Exporte, 21 `.modifications`, 18 CSV), siehe [TESTDATEN](TESTDATEN.md)

**Technik**
14. ~~Wie viele Kerne … einen frei lassen?~~ → alle minus einen, einstellbar (E6)
15. ~~Profiling-Dialog und Testdatengenerator?~~ → nur Entwickler-Werkzeuge (E13)
16. ~~Englische Code-Bezeichner mit Glossar?~~ → nein, deutsche Fachbegriffe, technische Begriffe englisch (E13)

---

## 8. Nächste Schritte (Stand nach den Entscheidungen vom 27.09.2026)
1. ~~**Testdaten**~~ → erledigt (27.09.2026): Inventar, Abdeckung und Referenz-Set R1–R10 in [TESTDATEN](TESTDATEN.md).
2. **Referenzdaten aus Delphi** (Phase 0): Zu jeder Testdatei in der Delphi-Version Plan-XML, CSV-Export und Teilkosten je Kostenart und Mannschaft erzeugen, dazu die Pläne/s-Basislinie auf Peters Rechner messen. Die Werte werden per Bildschirmsteuerung aus dem Kosten-Tab des installierten Originals abgelesen; Ablauf und Stand in [Referenz/README](../Referenz/README.md). **Erledigt: 25 Referenzfälle (R1–R26 ohne R23).** Offen: Pläne/s-Basislinie.
3. ~~**Solution-Gerüst** nach E5 anlegen~~ → erledigt (27.09.2026): `AndiGenerator.slnx` (.NET 10.0.401, Warnungen = Fehler, AOT-kompatible Bibliotheken, GPL-Header, `THIRD-PARTY-NOTICES.md`, Build-Ausgaben unter `%LOCALAPPDATA%\AndiGeneratorNeu`). **Phase 1 begonnen:** click-TT-Import (`ClickTtLeser`) und CSV-Export (`ClickTtCsvExport`); **Byte-Parität aller 17 vollständig terminierten CSV-Pläne** und Schichtenregeln (E5). **`.modifications` fertig** (27.09.2026): generischer Datenbaum `DatenKnoten` (1:1 `TDataObject`), click-TT → Basisbaum (`ClickTtNachPlanDaten`, inkl. Koppel-Zweitzeit, Doppelspieltage, Pflichtspielbereiche), Merge/Diff exakt nach Original, Lesen/Schreiben (`PlanDatenDatei`). Belegt durch Tests: **alle 21 `.modifications` byte-gleich zurückgeschrieben**, bei 20 Paaren `Diff(Merge(Basis, M), Basis) = M`, bei allen 21 stabiler Speicher-/Lade-Zyklus. **Optionsdatei fertig** (27.09.2026): Fachtypen `Gewichtung`, `MannschaftsKostenart`, `Rundenplanung`, `Berechnungsoptionen` (Domain), Datei `OptionenDatei` (Persistence, iso-8859-15) mit allen Lade-Eigenheiten des Originals (fehlendes `friday-is-part-of-weekend` = false, unbekannte Werte = Normal, jeder Fehler = Standard); **alle 26 gesicherten `.options` byte-gleich zurückgeschrieben**. **Gemerkte Pläne fertig** (27.09.2026): `GespeicherterPlanDatei` (`<plan><schedule><game …/>`, Sortierung und Nullpunkt 30.12.1899 wie im Original, ungültige Termine machen den Plan ungültig) und `Staffelordner` (Ordnername, `EncodeFileName`/`DecodeFileName`, Liste neueste zuerst); strenge Delphi-Parser (`StrToInt`, `DateFromString`, `TimeFromString`) zentral in `DelphiKompatibel`. Gegen eine mit dem Original gemerkte Datei (1. Kreisklasse, 110 Spiele, alle noch ohne Termin) geprüft: gleiche Länge, gleicher Inhalt; nur die Attributreihenfolge weicht ab – sie folgt im Original der Hash-Reihenfolge von `TDictionary` (abhängig von der Einfügegeschichte) und ist für das Format bedeutungslos, deshalb wird sie nicht nachgebildet. **Spiellokale fertig** (27.09.2026): `Spiellokale` (vorgegebenes Spiel → Heimspieltermin inkl. Koppel-/Auswärtskoppel-Zweitzeit → Mannschaft; nur „1“–„5“ gültig; Halbrunde filtert wie `isValidDateForWunschtermin`); **alle 17 CSV-Exporte mit selbst ermittelten Spiellokalen byte-gleich** („2. Kreisklasse.csv“ stammt aus dem Stand „(7)“). Nicht nachgebildet: Spiellokal aus bestehendem Spielplan (`OverrideLocation`), da click-TT dort immer leer liefert. **212/212 Tests grün.** **Analyzer eingeführt** (27.09.2026): StyleCop.Analyzers und SonarAnalyzer.CSharp für alle Projekte (`Directory.Packages.props`, `stylecop.json`, `.editorconfig`); Code angepasst (eine Klasse je Datei, vollständige XML-Doku öffentlicher Member, Member-Reihenfolge, `DateTimeKind`, LINQ statt Schleifen mit Bedingung), **0 Warnungen**, Analyzer-Warnungen brechen den Build wie alle anderen. Bewusst abgeschaltet: SA1101 (`this.`-Präfix), SA1623/SA1642 (englische Pflichtformulierungen, passt nicht zu E13), SA1600 in Tests. **Fachmodell fertig** (27.09.2026): typisierte Stammdaten in `AndiGenerator.Domain.Stammdaten` (`Staffel`, `Mannschaft`, `Heimspieltermin`, `Auswaertskoppel`, `Heimrechtvorgabe`, `Nachbarmannschaft`, `Spiel`, `Pflichtspielzeitraum`, `Setzliste` u. a.) und `StaffelAbbildung` (Persistence) für beide Richtungen: Lesen wie `TPlan.load`/`TMannschaft.load` (fehlende Werte = leer/0/false, Spiellokale bereinigt, Koppeltermin vor Doppelspieltag, `coupleprio`/`sameday`/`homeright` wie im Original ausgewertet, ungültige Termine = Fehler), Schreiben in der Form von click-TT-Import und `SetHomeDays`. Belegt: **alle 52 click-TT-Importe und alle 21 Stände mit `.modifications` überstehen Datenbaum → Fachmodell → Datenbaum inhaltsgleich** (Vergleich wie im Original: leeres Attribut = fehlendes; leere Listen vorgegebener Spiele/bestehender Spielplan = fehlende). Bewusst **nicht** im Fachmodell: abgeleitete Laufzeitdaten des Originals (erzeugte Spiele, Koppel-Zweittermine als eigene Wunschtermine, Vereinsgeschwister, sortierte Mannschaftsliste). Sie entstehen in Phase 2 beim Aufbau der `PlanDefinition` der Engine (E7). **241/241 Tests grün, 0 Warnungen. Phase 1 ist damit abgeschlossen**; GitHub-Repository/CI folgen später (E12). **Phase 2 begonnen** (27.09.2026): **Referenzmodell** `AndiGenerator.Engine.Referenz` – eine bewusst wörtliche Portierung von `TPlan.load`, `AssignOptions` (inkl. `ModifyTermineFromOptions`, `AssignSchedule`, `MarkNotNecessaryGames`), Runden, Spielgültigkeit und **allen 16 Mannschafts- sowie 5 Plan-Kostenarten und den harten Fehlern**, mit Delphi-`TDateTime` als `double` (gleiche Bildung wie `EncodeDate`+`EncodeTime`, damit Gleichheitsvergleiche übereinstimmen), ohne Caches. Öffentliche Schnittstelle `Referenzbewertung.Bewerten(Staffel, Berechnungsoptionen)` liefert den Inhalt des Kosten-Tabs; `Kostenanzeige` formatiert wie `MyFormatFloat(Short)`. **Paritätstests: alle 25 Referenzfälle R1–R26 stimmen vollständig mit dem Original überein** (Gesamtkosten, alle Plan-Kostenarten, sichtbare Spalten, jede Tabellenzelle „Anzahl /Gesamt (Kosten)“ und die Mannschaftssummen, jeweils in der Anzeigeformatierung). Vorgehen für den Rest von Phase 2: Das Referenzmodell ist der Maßstab; die schnelle, datenorientierte Engine (E7) wird dagegen mit vielen zufällig erzeugten Plänen verglichen (differenzielles Testen), zusätzlich zu den 25 Fällen. **266/266 Tests grün, 0 Warnungen.** **Optimierungsschritte im Referenzmodell** (27.09.2026): `FillTermine` (inkl. Auswärtskoppel-Pfad mit 70 %, Zweittermin-Bereinigung), `NeuWuerfeln` in allen vier Arten (Normal, Spieltag, Mannschaft, Raster mit Kreisverfahren), `FillPredefinedGames`, Merken/Zurücksetzen der besten Lösung (ohne Neusortierung wie im Original) und `RemoveNotValidDates`; dazu ein einsträngiger Worker `Referenzoptimierung` mit festem Startwert (Zufall wie Delphi `Random(n)`). Tests: Ausgehend vom leeren Plan entstehen für drei Staffeln mit fünf Strategien vollständige, gültige Pläne, deren Kosten bei erneuter Bewertung als bestehender Spielplan übereinstimmen; gleicher Startwert = gleiches Ergebnis. Das Referenzmodell schafft rund 150 Durchläufe pro Sekunde und ist damit nur Maßstab, nicht Produktivcode. **273/273 Tests grün.** **Schnelle Engine** (27.09.2026, `AndiGenerator.Engine.Kern`): `KernDefinition` (unveränderliche Stammdaten als Arrays, aus dem geladenen Referenzmodell aufgebaut, Sollspiele über Präfixsummen) und `KernPlan` (Zustand als Arrays, alle 16 Mannschafts- und alle Plan-Kostenarten, harte Fehler, alle Optimierungsschritte; im inneren Durchlauf keine Speicheranforderung). Rechenweg und Summationsreihenfolge folgen dem Referenzmodell, deshalb sind die Kosten **bitgleich** und gleiche Startwerte ergeben **dieselben Verläufe**. Öffentlich: `Kernbewertung.Bewerten`, `Kernoptimierung.Optimieren` (Meldungen für die Oberfläche weiterhin aus dem Referenzmodell). Differenzielle Tests (`KernDifferenzTests`): alle 27 Eingaben bitgleich bewertet; 216 zufällig veränderte Pläne (fehlende, verschobene, fremde, vertauschte, doppelte, entfernte Spiele) mit zufälligen Optionen (alle Rundenplanungen, Doppelrunde, Gewichtungen je Kostenart und Mannschaft) bitgleich, bei Ausnahmen werfen beide; 56 Optimierungsläufe (8 Staffeln, 5 Strategien, 2 Optionsvarianten) mit identischem Ergebnis. **Messung** (R2, Strategie 15, ein Thread, Release): Kern rund **51 000 Durchläufe/s**, Referenz rund 1 700/s, **Faktor 30** – noch ohne Kosten-Cache je Mannschaft (im Debug-Build rund 10 000/s). **573/573 Tests grün, 0 Warnungen.** Smart App Control blockierte neu gebaute Test-DLLs (Policy `{0283ac0f-…}`) und ist auf dem Entwicklungsrechner jetzt aus; als Ausweichweg ohne Abschalten gibt es `tests-wsl.cmd` (Doku/WSL-TESTS.md). Für die Auslieferung heißt das: Das fertige Programm braucht eine Code-Signatur einer vertrauenswürdigen Zertifizierungsstelle, sonst blockiert Smart App Control es bei Anwendern ebenso (Punkt für Release/E12). **Kosten-Cache je Mannschaft** (27.09.2026, `KernPlan.Cache.cs`): Vor jeder Kostenrechnung wird der Stand aller Spiele (Datum, Optionen, Hallenkapazität, nicht notwendig, Spiellokal) und aller Spiellisten mit dem Stand der letzten Rechnung verglichen; nur betroffene Mannschaften werden neu gerechnet, bei Hallenbelegung und parallelen Spielen auch deren Vereinsmannschaften. Die Summation bleibt unverändert, das Ergebnis bitgleich (Test: in 56 Läufen à 150 Durchläufe jede Kostenrechnung mit und ohne Cache verglichen; Verläufe weiterhin identisch zum Referenzmodell). Messung R2, ein Thread, Release (mit / ohne Cache / Referenz): R 30 500 / 30 300 / 1 600; 15 46 400 / 45 100 / 1 700; 5 78 300 / 69 600 / 1 800; S1,25 116 500 / 80 500 / 1 800; M2,10 100 300 / 88 200 / 1 700 Durchläufe/s. Der Cache bringt je nach Strategie 0–45 %; bei R und 15 dominiert nicht mehr die Kostenrechnung, sondern das Füllen der Termine. **629/629 Tests grün, 0 Warnungen.** Nebenbei behoben: `DelphiKompatibel.CodepagesRegistrieren` setzte das Merkerflag vor der Registrierung, sodass parallel laufende Tests iso-8859-15 zeitweise nicht fanden. **Inselmodell** (27.09.2026, `AndiGenerator.Engine.Inseln`, E6): `Inseloptimierer` mit der Topologie des Originals (Standard 4 Inseln × 14 feste + 10 dynamische Suchplätze), aber die Suchplätze sind keine eigenen Threads mehr: Kerne − 1 Rechen-Threads (einstellbar, Priorität BelowNormal) arbeiten ihre Suchplätze reihum ab. Jeder Suchplatz hat eigenen `KernPlan` und eigene Zufallsfolge (aus einem optionalen Startwert abgeleitet); alle Pläne teilen sich eine `KernDefinition`. Ein Koordinator gleicht alle 200 ms die Inseln ab (beste Lösung einsammeln und an alle Plätze der Insel verteilen, dynamische Plätze auf die erfolgreiche Strategie umstellen) und macht jeden fünften Abgleich den Optimierer-Schritt (Gesamtbestes übernehmen, schlechteste Insel nach 100 000 Durchläufen ohne Verbesserung leer neu starten). Austausch nur über kompakte Lösungen (`Loesung`: Termin und Terminangaben je Spiel) und Aufträge (`Volatile`/`Interlocked`), im inneren Durchlauf keine Sperre. Raster- und 100-%-Plätze werden wie im Original nach 10 000 Durchläufen gedrosselt (hier: nur jeder 50. Aufruf). Pause über `ManualResetEventSlim`, Stopp über `CancellationToken`. `Rechnen(n)` rechnet einsträngig mit festen Abgleichpunkten und ist mit Startwert reproduzierbar (Kommandozeile, Tests). **Befund #9 korrigiert** (Entscheidung Peter, 27.09.2026): die Drittel-Grenze der dynamischen Plätze zählt wirklich alle dynamischen Plätze. **Spezial-Insel** (Original `SpecialThread`, einschaltbar, Standard an): startet nach 3 Mio. Gesamtdurchläufen als fünfte Insel mit dem Gesamtbesten und einer zufällig gewählten, im Gesamtbesten Kosten verursachenden Kostenart in extremer Gewichtung (eigene `KernDefinition`, die Suchplätze erhalten neue Pläne per Auftrag). Wird die schlechteste Insel neu gestartet und hat die Spezial-Insel seit ihrem letzten Wechsel mindestens 200 000 Durchläufe gerechnet, startet die Insel mit der Spezial-Lösung (mit normalen Gewichten bewertet) und die Spezial-Insel wechselt die Kostenart; sonst startet die Insel leer. **Befund #10 korrigiert** (Entscheidung Peter, 27.09.2026): der Zähler der Spezial-Insel beginnt bei jedem Wechsel neu, leere Neustarts bleiben dadurch möglich. Tests: reproduzierbar mit Startwert, gültige und bessere Pläne für vier Staffeln (Kosten bei erneuter Bewertung gleich), Neustart der schlechtesten Insel, Parallelbetrieb, Pause. Messungen laufen jetzt in einer eigenen, nicht parallelen Testsammlung. **Messung R2** (Strategiemix aller 24 Suchplätze, Release, 12 logische Prozessoren): 1 Thread rund 74 000 Durchläufe/s, 11 Threads rund **433 000/s** (Skalierung 5,9 – entspricht etwa der Zahl physischer Kerne). Zum Vergleich: Der Autor nennt für das Original bis 40 000 Pläne/s (2. Kreisliga, 2015); eine Basislinie des Originals auf Peters Rechner steht noch aus. **639/639 Tests grün, 0 Warnungen.** **Kommandozeile** (27.09.2026, `tools/AndiGenerator.Cli`, `andigen optimieren <xml> [--sekunden N | --durchlaeufe N] [--kerne N] [--startwert N] [--optionen DATEI|auto] [--leer] [--ohne-spezial] [--intervall N] [--protokoll CSV] [--plan DATEI] [--ausgabe DATEI]`): lädt Staffel samt `.modifications` und Optionen (auch aus dem Staffelordner des Originals, nur lesend), optimiert mit dem Inselmodell, zeigt alle N Sekunden Durchläufe, Pläne/s, beste Kosten, Verbesserungen, Neustarts und die Kostenart der Spezial-Insel, schreibt den Verlauf optional als CSV und bewertet am Ende den besten Plan mit dem Referenzmodell (Plan-Kostenarten und Summe je Kostenart). Der beste Plan kann als gemerkter Plan im Format des Originals gespeichert werden (in den Staffelordner kopiert, erscheint er im Original unter „Gemerkte Pläne“). Aufruf per `optimieren.cmd` (XML-Datei daraufziehen), Ausgabe zusätzlich in `optimierung-log.txt`. Damit sind die Vergleiche aus Phase 3 möglich: Pläne/s gegen das Original auf demselben Rechner und die erreichten Kosten bis zur Konvergenz. **Erster Vergleich mit dem Original** (27.09.2026, Peters Rechner, 12 logische Prozessoren, 4. Kreisklasse Gruppe A, leerer Plan, 1 Minute): Original rund **65 000 Pläne/s**, Gesamtkosten nach 1 Minute rund 320 T; C# (11 Threads, Standardoptionen) rund **286 000 Pläne/s (Faktor 4,4)**, Gesamtkosten nach 1 Minute 300 T (321 T bereits nach 10 s). Die Kostenwerte sind nur eingeschränkt vergleichbar, solange nicht geklärt ist, ob das Original mit den Standardoptionen oder mit den gespeicherten Optionen der Staffel gerechnet hat (C#-Lauf dafür mit `--optionen auto` wiederholen); für die Qualität sind mehrere und längere Läufe nötig. Damit ist die Performance-Abnahme N4 (≥ 80 % der Pläne/s des Originals) deutlich erfüllt. **Feinoptimierung nach Messung** (28.09.2026, Test `Messungen.Phasen_messen` zerlegt einen Durchlauf in Zurücksetzen, Neu würfeln, Termine füllen und Kostenrechnung, dazu die Zeit je Kostenart): (1) Belegungszähler „Spiele je Mannschaft und Tag“ – `IstTerminFrei` beantwortet die meisten Anfragen ohne Durchlaufen der Spielliste; (2) Gültigkeitsprüfung nutzt den zugeordneten Wunschtermin statt ihn zu suchen; (3) Runde und „in zu planender Runde“ je Spiel und je Wunschtermin werden beim Setzen des Datums gemerkt statt an rund 20 Stellen neu bestimmt; (4) Spielverteilung: Koppelungen einmal je Mannschaft statt je Runde, Sollspiele über Präfixsummen je Tag statt Binärsuche. Alle Ergebnisse bitgleich (641/641 Tests, inkl. aller Vergleichstests gegen das Referenzmodell). Wirkung: Verbandsoberliga Strategie 15 von 92,6 auf **71,6 µs** je Durchlauf (−23 %), ungültige Spiele 10,2 → 2,9 µs, Spielverteilung 12,2 → 4,8 µs; Inselmodell R2 mit 11 Threads von 441 000 auf **644 000 Durchläufe/s** (+46 %). Größter verbleibender Posten ist das Füllen der Termine (~29 µs bei der Verbandsoberliga), danach viele Kostenarten mit je 1–4 µs. **Anwendungsschicht, erste Stufe** (28.09.2026, `AndiGenerator.Application`): `Plansitzung` (Ablauflogik des Original-Hauptfensters ohne Oberfläche: click-TT-Datei samt `.modifications` bzw. eigene Plandatei öffnen, Erststart erkennen, Optionen im Staffelordner laden/speichern, **„Standard nur für diese Sitzung“ ohne Überschreiben der gespeicherten Optionen (Befund #34 behoben)**, Rundenvorschlag Halbrunde/Rückrunde wie im Original ohne die fest einkodierte Corona-Abfrage 2020, gemerkte Pläne merken/auflisten/laden/entfernen nur im eigenen Staffelordner, Bewertung mit Meldungen, harte Fehler vor dem Export, CSV-Export für click-TT) und `Optimierungsdienst` (Starten mit leerem oder vorhandenem Plan, Pause, Pausenbereich für Dialoge, Datenänderung während der Generierung vom besten Plan aus, Stand mit Plänen, Pläne/s, Kosten, Verbesserungen, Zeit seit der letzten Verbesserung, Spezial-Kostenart und bestem Plan; Verbesserungen als Ereignis höchstens alle 500 ms). Dazu `Rundenpruefung` (Engine) für „Vor/Rück“ im CSV-Export. Tests mit Kopien in temporären Ordnern, u. a. CSV-Export byte-gleich zu einem echten Export des Originals. **651/651 Tests grün, 0 Warnungen.** Noch offen in der Anwendungsschicht: Schreiben geänderter Stammdaten als `.modifications` (kommt mit den Datendialogen, Phase 6), Programmeinstellungen (Kerne, Spezial-Insel), Updatecheck.
   - *Build-Umgebung:* Cloud-Umgebung ohne .NET/NuGet (Netzwerkrichtlinie), deshalb Build auf Peters Windows über `bauen.cmd` (von Claude per Explorer-Doppelklick gestartet, Protokoll `build-log.txt`). Die **Intelligente App-Steuerung** von Windows blockierte eine neu gebaute Test-DLL; die Architekturtests prüfen daher die `.csproj`-Dateien statt die DLLs zu laden. Bleibt das Problem bei weiteren Projekten, entscheidet Peter über Ausschalten bzw. Dev Drive.
**Oberfläche, Phase 4 abgeschlossen** (28.09.2026, E15): Avalonia 12 mit Dock.Avalonia (`AndiGenerator.Presentation` ViewModels ohne Avalonia, `AndiGenerator.UI` Views, `AndiGenerator.Desktop` = `AndiGeneratorNeu.exe`, Start per `starten.cmd`). Ansichten als andockbare Dokumente mit Planquelle je Ansicht (laufende Generierung, click-TT-Plan, gemerkte Pläne): Kosten (wie Original inkl. Hinterlegung, Gewichtungsmarken und Gewichtsänderung per Klick mit Meldungen je Kostenart), Qualität (Verstöße nach E14-Rangfolge), Terminplan. Öffnen wie im Original (Benutzereinstellungen weiterverwenden – Befund #34 behoben –, Rundenvorschlag, Erststart-Hinweise ohne Bearbeiten), Einstellungen als andockbares Dokument (Rundenplanung, Doppelrunde/Viertelrunden, Freitag/60 km, alle Gewichtungen; sofort gespeichert und von der laufenden Generierung übernommen), Start mit leerem, bestem, click-TT- oder gemerktem Plan, Plan merken, CSV-Export. **Eigener Datenordner** `%LOCALAPPDATA%\AndiGeneratorNeu`: Optionen und gemerkte Pläne des Originals werden beim ersten Öffnen nur kopiert, das Original bleibt unverändert. **Benchmark-Qualität:** Verbandsoberliga 91 Mio mit eigenen Gewichten (Original erreicht das nicht; `Testdaten/Benchmarks`, `Referenz/performance.md`). **675/675 Tests grün, 0 Warnungen.** Vorgemerkt (Peter): Die Dialoge sind funktional, aber schlicht – **modernere Gestaltung der Oberfläche später** (Ideen siehe Abschnitt 10). Offen: Ursache der geringen Pläne/s in der Oberfläche bei der Verbandsoberliga messen.

**Plan-Anzeige, Phase 5 abgeschlossen** (28.09.2026): alle Tabs des Original-`PlanPanel` als andockbare Dokumente. **Terminwünsche** (`Terminwunschauswertung`: Zeitachse mit Heimspielwünschen/Koppel-/Sperrterminen, je Mannschaft Wünsche mit Koppelpartner, max. Heimspielen, Ausweichtermin, Spiellokal, Hallenbelegung durch Nachbarn, parallele Nachbarspiele, spielfreie Tage, Sperrtermine, Auswärtskoppeln, 60 km, Heimrecht und Auswertung „reichen die Termine“ mit der Mindestzahl des Originals). **Termine der Nachbarmannschaften** (`Nachbarterminauswertung`, `PaintSisterTeams`). **Terminplan** in drei Darstellungen mit allen Hinweisen von `PaintOneTermin`. **Diagramme** (`Diagrammauswertung` + `Diagrammzeichnung`, `PaintVerteilung`): Spieltage mit Überlappungen, Wechsel Heim/Auswärts, Spielverteilung mit Koppel-Quadraten, Abstand Heimspiel zu Auswärtsspiel, Spiele pro Woche mit maximaler Differenz, Setzliste – Koordinaten und Farben wie im Original. Terminwünsche und Nachbarmannschaften hängen nur von Stammdaten/Optionen ab (je ein Dokument), Terminplan und Diagramme vom gewählten Plan (beliebig viele). **688/688 Tests grün, 0 Warnungen.** Visuelle Abnahme gegen das Original steht noch aus.

4. Später: Klasse-3-Einzelentscheidungen (E9) nach grünen Paritätstests; Signierung (E12) vor der öffentlichen Freigabe.

---

## 9. Dokumentenverzeichnis (Einzeldokus)
| Datei | Kategorie | Aufwand |
|---|---|---|
| [PlanTypes](Dateien/PlanTypes.md) | Kern: Datenmodell, Kostenfunktion, Füll-Algorithmus, Worker-Thread | XL |
| [PlanOptimizer](Dateien/PlanOptimizer.md) | Optimierung/Threading (oberste Ebene) | M |
| [PlanMainThread](Dateien/PlanMainThread.md) | Optimierung/Threading (Insel) | M |
| [PlanDataObjects](Dateien/PlanDataObjects.md) | Persistenz: XML-Datenbaum, click-TT-Import, Diff/Merge | M |
| [PlanUtils](Dateien/PlanUtils.md) | Hilfsfunktionen | S |
| [AndiGenerator](Dateien/AndiGenerator.md) | Programmstart (.dpr), TODO-Liste, Performance-Historie | S |
| [AndiGeneratorMain](Dateien/AndiGeneratorMain.md) | Hauptfenster, Programmablauf, Dateien | L |
| [PlanPanel](Dateien/PlanPanel.md) | Plan-Anzeige (5 Tabs, Diagramme, Kosten-Heatmap) | L |
| [PrintUtil](Dateien/PrintUtil.md) | Zeichen-/Druckschicht | M |
| [DialogPanel](Dateien/DialogPanel.md) | Basis Panel-Framework | S |
| [DialogTeamPanel](Dateien/DialogTeamPanel.md) | Basis Panel pro Mannschaft | S |
| [DialogPanelMultiTeams](Dateien/DialogPanelMultiTeams.md) | Master/Detail-Container | S–M |
| [DialogForMultiplePanel](Dateien/DialogForMultiplePanel.md) | Host „Spielplandaten" (13 Seiten) | M |
| [DialogForSinglePanel](Dateien/DialogForSinglePanel.md) | Host für eine Seite | S |
| [DialogOptions](Dateien/DialogOptions.md) | Einstellungen/Gewichtungen | M |
| [DialogPanelOptionArray](Dateien/DialogPanelOptionArray.md) | Gewichtungs-Steuerelement | S |
| [DialogForSingleGewichtungsOptions](Dateien/DialogForSingleGewichtungsOptions.md) | Gewichtung einzeln | S |
| [DialogPanelMainData](Dateien/DialogPanelMainData.md) | Seite Stammdaten | S |
| [DialogPanelTeams](Dateien/DialogPanelTeams.md) | Seite Mannschaften | M |
| [DialogPanelHomeDays](Dateien/DialogPanelHomeDays.md) / [Detail](Dateien/DialogPanelHomeDaysDetail.md) | Seite Heimspieltermine | S / M |
| [DialogPanelHomeCoupleCompact](Dateien/DialogPanelHomeCoupleCompact.md) / [Detail](Dateien/DialogPanelHomeCoupleCompactDetail.md) | Seite Heim-Koppeltermine | S / M |
| [DialogPanelAuswaertskoppel](Dateien/DialogPanelAuswaertskoppel.md) / [Detail](Dateien/DialogPanelAuswaertsKoppelDetail.md) | Seite Auswärtskoppel | S / M |
| [DialogPanelLocationsCompact](Dateien/DialogPanelLocationsCompact.md) / [Detail](Dateien/DialogPanelLocationsCompactDetail.md) | Seite Spiellokale | S / M |
| [DialogPanelHomeRight](Dateien/DialogPanelHomeRight.md) / [Detail](Dateien/DialogPanelHomeRightDetail.md) | Seite Heimrecht | S / S |
| [DialogPanel60km](Dateien/DialogPanel60km.md) / [Detail](Dateien/DialogPanel60kmDetail.md) | Seite 60-km-Regel | S / S |
| [DialogPanelSisterTeams](Dateien/DialogPanelSisterTeams.md) / [Detail](Dateien/DialogPanelSisterTeamsDetail.md) | Seite Nachbarmannschaften | S / S–M |
| [DialogPanelFreeDays](Dateien/DialogPanelFreeDays.md) | Seite spielfreie Tage | S |
| [DialogPanelMandatoryDays](Dateien/DialogPanelMandatoryDays.md) | Seite Pflichtspieltage | S–M |
| [DialogPanelPredefinedGames](Dateien/DialogPanelPredefinedGames.md) | Seite vorgegebene Spiele | S–M |
| [DialogPanelRanking](Dateien/DialogPanelRanking.md) | Seite Setzliste | S |
| [DialogEditOneHomeDay](Dateien/DialogEditOneHomeDay.md) | Einzel-Dialog Heimtermin | M |
| [DialogEditOneAuswaertsKoppel](Dateien/DialogEditOneAuswaertsKoppel.md) | Einzel-Dialog Auswärtskoppel | S |
| [DialogEditOneGame](Dateien/DialogEditOneGame.md) | Einzel-Dialog Spiel | S |
| [DialogEditOneSisterGame](Dateien/DialogEditOneSisterGame.md) | Einzel-Dialog Nachbarspiel | S |
| [DialogEditSisterTeam](Dateien/DialogEditSisterTeam.md) | Dialog Nachbarmannschaft | M |
| [DialogEditTeamName](Dateien/DialogEditTeamName.md) | Dialog Mannschaftsname | S |
| [EditMandatoryDatesDialog](Dateien/EditMandatoryDatesDialog.md) | Dialog Pflichtspieltage | S |
| [DialogFirstStart](Dateien/DialogFirstStart.md) | Erststart-Assistent | S |
| [DialogSavePlan](Dateien/DialogSavePlan.md) | Plan speichern | S |
| [DialogPrintSelect](Dateien/DialogPrintSelect.md) | Druckauswahl | S |
| [DialogQuestionHardErrors](Dateien/DialogQuestionHardErrors.md) | Rückfrage harte Fehler | S |
| [DialogQuestionUseUserOptions](Dateien/DialogQuestionUseUserOptions.md) | Rückfrage Benutzeroptionen | S |
| [DialogGetUpdates](Dateien/DialogGetUpdates.md) | Updatecheck | S |
| [DialogProfile](Dateien/DialogProfile.md) | Entwickler: Benchmark/Statistik | S/M |
| [DialogCreateTestData](Dateien/DialogCreateTestData.md) | Entwickler: Testdaten (toter Code) | – |
| Projekt/Build: [dproj](Dateien/AndiGenerator.dproj.md), [dproj.local](Dateien/AndiGenerator.dproj.local.md), [dsk](Dateien/AndiGenerator.dsk.md), [identcache](Dateien/AndiGenerator.identcache.md), [res](Dateien/AndiGenerator.res.md), [Icon1](Dateien/AndiGenerator_Icon1.ico.md), [Icon2](Dateien/AndiGenerator_Icon2.ico.md), [manifest](Dateien/andigenerator.manifest.md), [manifest32](Dateien/andigenerator32.manifest.md), [manifest64](Dateien/andigenerator64.manifest.md), [tvsconfig](Dateien/AndiGenerator_project.tvsconfig.md), [optset](Dateien/temp.optset.md) | Build/IDE | S / – |
| Drittbibliothek: [FastMM4.pas](Dateien/FastMM4.pas.md), [FastMM4Messages.pas](Dateien/FastMM4Messages.pas.md), [FastMM4Options.inc](Dateien/FastMM4Options.inc.md) | Speichermanager | entfällt |

---

## 10. Ideen aus der TT-Spielplan Engine (Stand 28.09.2026)

Die **TT-Spielplan Engine** (Stephan Rüb, v1.1.2) ist ein neues, unabhängiges Programm für dieselbe Aufgabe: Python-Backend mit Google OR-Tools CP-SAT, Oberfläche als lokale Web-App (React), Einlesen der click-TT-XML, Export als XML/CSV. Untersucht wurden nur die Programmbestandteile und die Oberflächentexte, nicht die Programmlogik; eine Lizenz ist nicht angegeben – Ideen werden übernommen, Code nicht.

**Qualitätsvergleich** (4. Kreisklasse Gruppe A als Halbrunde, bewertet mit `andigen bewerten` im Kostenmodell des Originals): TT-Spielplan Engine 1,3 Mio (Standard-Zeitlimit 15 s), AndiGenerator (neue Engine, 2 min) 66 T. Unterschied vor allem bei Spieltagsstruktur (die TT-Engine bildet keine kompakten Spieltage) und Pflichtspielwoche (5 Mannschaften ohne Spiel); bei Sperrtermin-Verstößen, Doppelwochen und Heim-/Auswärtswechsel lag die TT-Engine leicht vorn. Einschränkung: Sie kannte vermutlich die `.modifications` (Sperrtermine, Nachbartermine) nicht. Nicht weiter verfolgt (Entscheidung Peter).

**Merkposten:**
1. **Oberflächenkonzepte** (Kandidaten für Phase 4/5): Top-5-Pläne mit „ab hier optimieren“; Planqualität als Zahl der Verstöße je Kriterium (live, Ziel 0; passt zu E14); Diagramme (Heim-/Auswärts-Rhythmus je Mannschaft, Spiel- und Pausenrhythmus, Auslastung je Kalenderwoche); Gewichtungs-Voreinstellungen; Konfliktliste; Terminwünsche mit Erfüllungsstatus; Kalender mit Verschieben per Klick; Verlauf früherer Läufe; Batch-Modus für mehrere Staffeln; Importe (Rahmenterminplan-PDF, Vereinsbemerkungen-PDF, Setzliste aus TTR-CSV); Plausibilitätsprüfung der Daten vor dem Start; geführte Einführung (Tour).
2. **Andere Optimierungsverfahren prüfen**, insbesondere **Google OR-Tools CP-SAT** (Apache-2.0, GPL-v3-kompatibel; .NET-Paket `Google.OrTools`, nativ ca. 35 MB): z. B. als Startlösung oder für Teilprobleme (Paarungen/Heimrecht, Pflichtspielwoche) in Kombination mit der bestehenden lokalen Suche. Maßstab bleibt das Kostenmodell des Originals (`andigen bewerten`).

## Änderungsprotokoll
| Datum | Änderung |
|---|---|
| 27.09.2026 | Erstfassung nach vollständiger Code-Analyse; alle Entscheidungen offen bzw. vorgeschlagen |
| 27.09.2026 | E1 entschieden: Desktop in Release 1, Architektur tablettauglich |
| 27.09.2026 | E2 entschieden: Avalonia 12; TreeDataGrid ausgeschlossen (kommerzielle Lizenz) |
| 27.09.2026 | E3 entschieden: AXAML-Views + C#-ViewModels, kompilierte Bindings |
| 27.09.2026 | E4 entschieden: .NET 10 LTS, ReadyToRun self-contained, AOT-kompatibel |
| 27.09.2026 | E5 entschieden: Schichtenarchitektur wie vorgeschlagen |
| 27.09.2026 | E6 entschieden: skalierendes Inselmodell, Kerne−1, optionaler Seed |
| 27.09.2026 | E7 entschieden: datenorientiertes Engine-Modell, exakte Kostenparität |
| 27.09.2026 | E8 entschieden: Originalformate lesen + schreiben, Optionen iso-8859-15, CSV Windows-1252 |
| 27.09.2026 | E9 entschieden: nach Klassen; Limits aufgehoben; rpCorona bleibt |
| 27.09.2026 | E10 entschieden: PDF-Export + Tabellenexport (CSV/xlsx) |
| 27.09.2026 | E11 entschieden: öffentlich unter GPL v3, Kontakt zum Autor besteht |
| 27.09.2026 | E12 entschieden: GitHub Releases + Velopack; Signierung vor Freigabe entscheiden |
| 27.09.2026 | E13 entschieden: deutsche Fach-Bezeichner, .resx (nur Deutsch), Dev-Tools nur in Benchmarks/CLI |
| 27.09.2026 | Fragen 9, 10, 13 beantwortet; Abschnitt 8 auf nächste Schritte aktualisiert |
| 27.09.2026 | Testdaten analysiert (TESTDATEN.md); Frage 12 teilweise beantwortet; Befund #30 ergänzt |
| 27.09.2026 | E7 angepasst: Parität gegen Original-Exe – Anzahlen exakt, Kosten gleich nach Anzeige-Formatierung |
| 27.09.2026 | Referenzwerte R1–R3 abgelesen (Referenz/); Befund #31 (Druckerwechsel) ergänzt |
| 27.09.2026 | 25 Referenzfälle vollständig; Frage 12 beantwortet (click-TT akzeptiert Delphi-CSV); Befund #32 (Namen trimmen) |
| 27.09.2026 | Version 26.7.1.0 als Referenz (Abschnitt 1.5, nur Stabilitätskorrekturen); N4 präzisiert (keine Technik mit deutlich schlechterer Grundleistung) |
| 27.09.2026 | E14 vorgemerkt: automatische Gewichtungsanpassung (Ziel: weniger Verstöße), Architektur-Vorkehrungen festgelegt |
| 27.09.2026 | E14: Rangfolge „besserer Plan“ nach Peter festgelegt (A: Muss, B: Sperr-/Ausweichtermine, C: Rest) |
| 27.09.2026 | E14/A5: letzter Pflichtspieltag; Regel für ungerade Mannschaftszahl (spielfreie Mannschaft am vorletzten Spieltag); Befund #33 |
| 27.09.2026 | E14 entschieden: vollautomatische Sonderfunktion nach Herstellung der Originalfunktion (Phase 10) |
| 27.09.2026 | Abnahme Phase 3/10 präzisiert: Performance (kurze Läufe) und Optimierungsqualität (bis zur Konvergenz, komplexitätsabhängig) getrennt |
| 27.09.2026 | Phase 2: Optimierungsschritte (Füllen, Neu-Würfeln, Raster) im Referenzmodell, 273/273 Tests |
| 27.09.2026 | Phase 2 begonnen: Referenzmodell der Kostenrechnung, Parität 25/25 Referenzfälle, 266/266 Tests |
| 27.09.2026 | Phase 1 abgeschlossen: typisiertes Fachmodell (Stammdaten) und Abbildung Datenbaum ↔ Fachmodell, 73 Round-Trips inhaltsgleich, 241/241 Tests |
| 27.09.2026 | StyleCop- und Sonar-Analyzer eingeführt, 0 Warnungen, 212/212 Tests |
| 27.09.2026 | Phase 1: Spiellokale im CSV-Export, 17/17 byte-gleich, 212/212 Tests |
| 27.09.2026 | Phase 1: gemerkte Pläne und Staffelordner (gegen Originaldatei geprüft), Befund #35, 182/182 Tests |
| 27.09.2026 | Phase 1: Optionsdatei (`AndiGenerator.options`) lesen/schreiben, 26/26 byte-gleich, 163/163 Tests |
| 27.09.2026 | Phase 1: `.modifications` (Datenbaum, Merge/Diff, Lesen/Schreiben) fertig, 149/149 Tests |
| 27.09.2026 | Phase 1 begonnen: Solution-Gerüst, click-TT-Import, CSV-Export (Build per `bauen.cmd` auf Windows); XSD nicht verfügbar; Performance M1/M2 gemessen |
| 28.09.2026 | E15 entschieden: Andock-Layout (Dock.Avalonia), mehrere Ansichten gleichzeitig, Planquelle je Ansicht |
| 28.09.2026 | Kommandozeile `andigen bewerten` (fremde Pläne mit dem Kostenmodell des Originals vergleichen) und `--rundenplanung`; Abschnitt 10: Ideen aus der TT-Spielplan Engine, CP-SAT als zu prüfendes Verfahren |
| 28.09.2026 | Phase 4 abgeschlossen (Öffnen-Ablauf, Einstellungen, Startplan, eigener Datenordner), Phase 5 begonnen (Kosten/Qualität), Benchmark Verbandsoberliga 91 Mio, 675/675 Tests |
| 28.09.2026 | Phase 5 abgeschlossen: Terminwünsche, Nachbarmannschaften, Terminplan, Diagramme; 688/688 Tests |
| 29.09.2026 | Phase 7 begonnen: Druckauswahl und PDF-Ausdruck (Text- und Tabellenabschnitte, Seitenumbruch zwischen Zeilen, Tabellenkopf je Seite), Tippfehler des Originals in Überschriften korrigiert („letzter“, „Nachbarmannschaften“) |
| 29.09.2026 | Phase 7b: Terminwunschraster und Diagramme im Ausdruck, gemeinsame Zeichenroutine für Bildschirm und PDF |
| 29.09.2026 | Dialog „Über“ (Original Andreas Hofmann, Portierung Peter Buchmann, GPL-3.0 §5d), Doku/ENTSTEHUNG.md, Urheberangaben in LIZENZ.md und Directory.Build.props |
| 29.09.2026 | E16: Programmname AndiGenerator.NET (Titel, Über, Programmdatei, Dateiköpfe, Datenordner mit Übernahme aus AndiGeneratorNeu) |
| 29.09.2026 | Dateiköpfe auf SPDX/REUSE umgestellt (beide Urheber je Datei, Prüfung durch StyleCop) |
| 29.09.2026 | Nachtrag Phase 4: „Neu…“ (Original „Neue Datei zur komplett manuellen Eingabe erstellen…“, `MenuNewClick`) – leere Plandatei anlegen, öffnen, Spielplandaten-Dialog zeigen |
| 29.09.2026 | Nachtrag Phase 5: Zoom der Ausgabeansichten (Original: gemeinsame Zoomstufe 25–200 % im Hauptfenster; hier je Ansicht 25–300 %, Schieberegler in 5-%-Schritten mit Prozentknopf (Klick = 100 %), Stufen wie im Browser für Tasten und Mausrad, Strg + Mausrad bzw. Zwei-Finger-Zoom auf dem Touchpad, Strg + Plus/Minus/0 wie im Browser für die aktive Ansicht) |
| 29.09.2026 | Phase 7: Excel-Export „Nach Excel…“ in den Planansichten (`Persistence.Tabellen.XlsxDatei`, ohne Fremdbibliothek; Blätter Übersicht, Spielplan, Mannschaftspläne, Kosten) |
| 29.09.2026 | Kostenansicht: bedingte Plan-Kostenarten wie im Original nur bei Kosten > 0; neue Dock-Ansicht „Meldungen“ (Original: Meldungen je Mannschaft unter der Kostentabelle), mit Auswahl einer Mannschaft |
| 29.09.2026 | Neue Oberfläche nach Entwurf (Touch, Symbole, Struktur): Navigation links mit Symbolen (bei schmalem Fenster nur Symbole), Kopf mit Staffel und einem Knopf Start/Pause/Fortsetzen, Kacheln mit dem Stand der Generierung, Startseite mit „Zuletzt geöffnet“ (neu), Bedienflächen ≥ 44 px, eigenes Farb- und Schriftkonzept (`Stil.axaml`), Symbole ohne Fremdbibliothek (`Symbol`) |
| 29.09.2026 | Ansichten: zu Beginn nur „Kosten“; ein Klick links holt die Ansicht nach vorn oder öffnet sie (je Art eine); Planansichten ohne Plan, Terminwünsche/Nachbarmannschaften ohne Staffel deaktiviert; vorn liegende Ansicht links hervorgehoben |
| 29.09.2026 | Programmsymbol `AndiGenerator.NET.ico` (Schläger mit Ball auf Blau, 16–256 px) für die .exe, alle Fenster und die Taskleiste |
| 29.09.2026 | Anpassung an die Fenstergröße: Kacheln 2 × 2 unter 1000 px Breite, Kopf und Kacheln kompakt unter 820 px Höhe; Start-Zoom der Ansichten nach verfügbarem Platz (75–175 %); Programm startet maximiert |
| 30.09.2026 | Einrichtungsseite nach dem Öffnen einer Staffel statt der Dialogkette (Benutzereinstellungen, Rundenplanung, Erststart-Hinweise): Checkliste links, Bearbeitung rechts, „Übernehmen“/„Überspringen“ je Schritt, „Abschließen“ bzw. „Abschließen und generieren“; kein eigener Navigationspunkt. Erststartdialog entfernt |
| 30.09.2026 | Einrichtung: Erklärtext, Details und Bearbeitungsseite scrollen gemeinsam, damit die Seite (z. B. Setzliste per Ziehen) den ganzen Platz bekommt |
| 30.09.2026 | Einrichtung: eingebettete Seiten im Stil der Anwendung (große Knöpfe mit Symbolen, Listen und Auswahlfelder ≥ 44 px, Abschnittsüberschriften, Zeilen mit Trennlinien, doppelte Erklärung der Setzliste ausgeblendet); Grundformen auch im Datendialog |
| 30.09.2026 | Kostenkachel: Nach einer Änderung von Gewichten, Optionen oder Spielplandaten während der Generierung bezieht sich die prozentuale Verbesserung auf den ersten gültigen Plan nach der Änderung („seit der letzten Kostenanpassung“) |
| 30.09.2026 | Kacheln Kosten und Pflichtregeln: „Details ›“ rechts oben neben dem Titel; der Hinweistext darunter nutzt die volle Breite und bricht bei Bedarf in eine zweite Zeile um |
| 30.09.2026 | Kopf: „Plan merken“ direkt neben der Startplan-Auswahl; neuer Knopf „Schließen“ (nicht im Original) beendet die Generierung (mit Rückfrage, wenn schon ein Plan vorliegt), schließt alle Ansichten und zeigt wieder die Startseite |
| 30.09.2026 | Navigation: ohne geöffnete Staffel ist keine Ansicht hervorgehoben (nach „Schließen“ war „Kosten“ noch markiert) |
| 30.09.2026 | Dialoge im Stil der Anwendung (Klasse „dialog“): weiße Fläche, 15 pt, Felder ≥ 40 px, Fußleiste mit Trennlinie; „Abbrechen“/„Nein“ links daneben, Bestätigung (OK, Ja, PDF erzeugen, Schließen) als blauer Knopf ganz rechts. Betrifft Meldung/Frage/Texteingabe, Gewichtung, Spielplandaten und alle Bearbeitungsdialoge |
| 30.09.2026 | Dialoge öffnen erst, wenn der auslösende Klick vollständig verarbeitet ist (sonst Windows-Fehlerton und blinkender Dialog, z. B. bei Drucken und Über) |
| 30.09.2026 | Während der Einrichtung sind links Spielplandaten, Einstellungen, alle Ansichten und Drucken gesperrt, und keine Ansicht ist hervorgehoben; Öffnen und Neu bleiben möglich |
| 30.09.2026 | Einstellungen im Stil der Anwendung: Titel mit „Standard wiederherstellen…“, Bereiche als Karten (nebeneinander, soweit Platz ist), Gewichtungen als Zeilen mit Trennlinie, Auswahlfelder ≥ 44 px |
| 30.09.2026 | Spielplandaten im Stil der Anwendung: Seitenliste links wie die Navigation, Seiten mit großen Bedienelementen, Knöpfen mit Symbolen, Spaltenköpfen und Trennlinien; Dialog 1360 × 820 (an kleinere Bildschirme angepasst); Wunschtermine mit schmaleren Tagesfeldern (88 px), damit die ganze Woche sichtbar ist |
| 30.09.2026 | Wunschtermine: Datumsbereich der Woche bricht in zwei Zeilen um statt abgeschnitten zu werden |
| 30.09.2026 | Spielfreie Tage wieder untereinander (die Spalten liefen im waagerecht scrollbaren Dialog in eine einzige Zeile) |
| 30.09.2026 | Restliche Ansichten angeglichen: Terminwünsche mit Leiste (Zoom) und Legende als Karte; Kosten-Legende als Karte; Farben der Qualitäts-/Pflichtanzeige, Terminplanzeilen, Terminwunschtexte, Gewichtungsmarken und Terminwunschdiagramm (Bildschirm und PDF) in den Tönen der Gestaltung statt reinem Rot/Grün/Blau |
| 30.09.2026 | Startseite: Einträge der Liste „Zuletzt geöffnet“ lassen sich mit × entfernen (nur aus der Liste, die Datei bleibt erhalten) |
| 30.09.2026 | Generierung startet nur mit mindestens zwei Mannschaften (sonst Hinweis auf „Spielplandaten“); Staffel ohne Namen zeigt im Kopf den Dateinamen |
| 30.09.2026 | Öffnen einer anderen Staffel beendet eine laufende oder angehaltene Generierung auch in der Anzeige (vorher blieb „Fortsetzen“ stehen) |
| 30.09.2026 | „Start mit“ nur vor dem Start wählbar (ausgegraut, solange eine Generierung läuft oder angehalten ist; Tooltip erklärt „erst Beenden“) |
| 30.09.2026 | Start mit gemerktem Plan (und nach Datenänderung): der Ausgangsplan wird sofort als bester Plan gemeldet – Ansichten und Pflichtregeln sind nicht mehr leer, bis er übertroffen wird |
| 30.09.2026 | Kostenansicht: Legende „So liest man die Tabelle“ als aufklappbare Karte (Info-Symbol, Akzentfarbe), anfangs zugeklappt; Excel-Export ohne Legende auf dem Blatt „Kosten“ |
| 30.09.2026 | Reiter der Ansichten im Stil der Anwendung: transparent auf dem Grund, gewählter Reiter weiß mit abgerundeten oberen Ecken und halbfett, aktive Gruppe mit Akzentlinie und Akzentschrift statt vollflächigem Blau |
| 30.09.2026 | Qualitätsansicht im Stil der Anwendung: Zusammenfassung als Karte mit Symbol, Tabelle als Karte mit Spaltenköpfen in Großbuchstaben, Zeilen mit Trennlinie (≥ 44 px), Stufe als Marke, Zustand als farbiger Kreis; ohne Plan nur der Hinweis |
| 30.09.2026 | Bildlaufleisten systemweit neben dem Inhalt statt darüber (AllowAutoHide aus für ScrollViewer, ListBox, TextBox, TreeView) |
| 30.09.2026 | Qualitätsansicht: ein gemeinsamer Bildlauf für die Tabelle (Leiste rechts neben der Karte statt über der Kostenspalte) |
| 30.09.2026 | Kostenansicht: Kennzahlen als Kacheln (Name klein, Wert groß; Gesamtkosten hervorgehoben; Gewichtungsmarke und Klick zum Ändern wie bisher) statt als Textraster |
| 30.09.2026 | Kostenansicht neu gestaltet (Entwurf „Kostensteuerung“): Matrix Mannschaften × Kostenarten, beide nach Kosten sortiert; senkrechte Spaltenköpfe; Kostenarten ohne Kosten ausblendbar; Zellen wahlweise Kosten oder Verstöße, eingefärbt nach Anteil; Balken „Wo stecken die Kosten?“; rechts Details (Werte, Meldungen) mit Gewichtung zum direkten Ändern statt Gewichtungsdialog. Ausdruck und Excel behalten die klassische Tabelle |
| 30.09.2026 | Kostenansicht: Details rechts nur nach einem Klick, mit × schließbar; Umschalter Kosten/Verstöße in die Matrixkarte (Leiste wieder schmal genug für den Zoom); Kostenarten ohne Kosten über eine schmale Spalte direkt hinter den Kostenarten ein- und ausblenden |
| 30.09.2026 | Schrift anwendungsweit kleiner: Grundschrift 13 statt 14/15 (Fenster und Fluent-Steuerelemente), alle festen Schriftgrößen der Oberfläche um 1–2 Punkte reduziert (15→13, 14→13, 13→12, Überschriften −2); Bedienflächen bleiben ≥ 44 px; PDF-Ausdruck unverändert |
| 30.09.2026 | Meldungsansicht als Kacheln: je Mannschaft eine Karte mit Name, Anzahl und den Meldungen, nebeneinander umbrechend, Mannschaften mit den meisten Meldungen zuerst |
| 30.09.2026 | Terminplanansicht neu gestaltet: Umschalter Spielplan / Mannschaftspläne / mit Nachbarmannschaften; Spiele ohne Termin gesammelt oben; Spielplan als Wochenkarten mit Rundenüberschriften, Datumsmarke und Hinweis-Chips (rot Verstoß, ocker beachten, blau Info, grau Spiellokal); Mannschaftspläne als Kacheln mit Heim/Auswärts-Folge, H/A-Marke und eingerückten Nachbarspielen; Details eines Spiels rechts (schließbar). Ausdruck, Excel und die Textzeilen bleiben unverändert |
| 30.09.2026 | Diagrammansicht: Auswahlleiste mit einer Taste je Diagramm und „Alle“; standardmäßig ein Diagramm in einer Karte mit einem erklärenden Satz statt aller untereinander (Zeichnung unverändert, PDF unverändert) |
| 30.09.2026 | Diagramme: „Alle“ entfällt, immer ein Diagramm; Zeichnung in den Farben der Gestaltung (Bildschirm und PDF): Heimspiel Akzentblau gefüllt, Auswärtsspiel hohl/grau, Überlappungen der Spieltage als Rotton, ruhige Zeilenbänder und Rasterlinien, Beschriftungen grau, Setzliste grün–ocker–orange–rot in Designtönen; in der Ansicht ohne doppelten Titel |
| 30.09.2026 | Terminwünsche: Umschalter Übersicht / Je Mannschaft; Übersicht mit dem Raster in einer Karte und den allgemeinen Hinweisen als Hinweiskarte; je Mannschaft eine Karte mit Statusmarke (Probleme / Halle belegt / genügend Termine), Abschnitten nach den Zwischenüberschriften des Originals und farbig hinterlegten Hervorhebungen; Mannschaften mit Problemen zuerst |
| 30.09.2026 | Nachbarmannschaften: je Mannschaft eine Karte mit Bilanz („12 Spiele an 8 Tagen“) und Marke für direkte Nachbarn, darin je Tag Datumsmarke und die Spiele mit Uhrzeit, Begegnung und Spiellokal; direkte Nachbarn akzentblau hervorgehoben; Legende als Karte in der Leiste |
| 30.09.2026 | Schrift IBM Plex Sans eingebettet (Regular, Medium, SemiBold, Bold unter Assets/Fonts mit OFL.txt; übrige Schnitte entfernt), Segoe UI als Ersatz; Eintrag in THIRD-PARTY-NOTICES und im Über-Dialog. IBM Plex Mono liegt nicht vor, Zahlen weiter in Consolas |
| 30.09.2026 | Auch IBM Plex Mono eingebettet (Regular, Medium, SemiBold, Bold; übrige Schnitte entfernt) für Zahlen in Kacheln und Tabellen, Consolas als Ersatz |
| 30.09.2026 | Über-Dialog: Andreas Hofmann ausführlicher gewürdigt (Optimierung mit Inseln, Kostenfunktion, click-TT-Import, Terminwunsch- und Nachbarauswertung, Diagramme, Dateiformate); die Bedienung wird nicht mehr ihm zugeschrieben, sondern als neu gestaltet genannt |
| 30.09.2026 | PDF-Ausdruck im Design der Oberfläche: IBM Plex Sans eingebettet (einmal je Dokument), Farben der Gestaltung (`Farbe` mit den Tönen aus `Stil.axaml`), Titel fett mit Akzentstrich, Spaltenköpfe grau; Kennzahlen als Kacheln; Kostentabelle als Matrix mit senkrechten Spaltenköpfen, zentrierten Werten, Zeilenlinien und der Einfärbung der Kostenansicht (auch im Excel-Export, dort mit weißer Schrift auf kräftigem Rot); Hinweise im Terminplan rot/ocker/grau wie die Chips; Terminwunschtexte und -raster in den Designtönen |
| 30.09.2026 | Kostenansicht kompakter, damit mehr Matrix sichtbar ist: Reiter aller Ansichten flacher (34 statt 44 px), Kennzahlen als flache Chips in der Leiste, Umschalter Kosten/Verstöße in die Leiste, Verteilung „Wo stecken die Kosten?“ als eigene Karte neben der Matrix (bei zu wenig Breite darunter, alle Anteile mit Prozent in der Legende), Matrixzeilen 28 statt 40 px, Spaltenköpfe senkrecht mit Höhe nach dem längsten Namen statt fest 170 px (schräge Köpfe ausprobiert und verworfen) |
| 30.09.2026 | Anleitung neu: `Doku/ANLEITUNG.md` (13 Kapitel; Bedienung neu für die neue Oberfläche, Fachteil nach der Originalanleitung in eigenen Worten und aktualisiert), 17 Bilder aus anonymisierten Testdaten, PDF mit IBM Plex über pandoc und Chromium (`tools/anleitung`), wird mit dem Programm ausgeliefert und über „Anleitung“ in der Seitenleiste geöffnet; Hilfe für Bildschirmfotos: Strg+Umschalt+F12 speichert das aktive Fenster als PNG |
| 30.09.2026 | Öffentliches Repository github.com/PBuchmann/AndiGenerator.NET: README, LICENSE (GPL-3.0-Volltext, dazu `LICENSES/GPL-3.0-only.txt` für REUSE; `COPYING.txt` entfällt), `.gitattributes` (Testdaten byte-genau), `.gitignore` zusammengeführt, CI-Workflow „Build und Tests“ (Windows, .NET 10); vor dem ersten Commit alle Dateien gegen die private Zuordnung der Klarnamen geprüft: keine Treffer |
| 30.09.2026 | Phase 8 begonnen: Velopack 0.0.1298 (MIT) für Installation und Updates über GitHub Releases – `VelopackApp` im Programmstart, Updateprüfung nach dem Start (nur installiert; lädt im Hintergrund, fragt „jetzt neu starten“, sonst beim Beenden), Workflow `release.yml` (Tag `v*` → bauen, testen, win-x64 eigenständig veröffentlichen, `vpk pack` mit Setup, Portable-ZIP und Delta, `vpk upload github`), Version aus dem Tag (Standard 0.9.0). Signierung (E12): zunächst ohne, Antrag bei SignPath Foundation (kostenlos für Open Source) parallel; Anleitung und README beschreiben Installation, Warnhinweis und Updates |
