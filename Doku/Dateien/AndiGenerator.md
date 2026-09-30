# AndiGenerator (AndiGenerator.dpr, mit Bezug auf AndiGenerator.dproj)

**Kategorie:** Projekt/Build
**Umfang:** 97 Zeilen .dpr (davon ca. 70 Zeilen Kommentar mit TODO-Liste und Performance-Historie); keine .dfm
**Abhängigkeiten (uses):** `AndiGeneratorMain` (Hauptformular); RTL/VCL: `Vcl.Forms`, `FastMM4` (Speichermanager, nur wenn `DEBUG_MULTITHREAD` **nicht** definiert ist)
**Verwendet von:** – (Programm-Einstiegspunkt)

## Zweck
Das ist das Hauptprogramm der Delphi-VCL-Anwendung. Es initialisiert die `Application`, erzeugt das Hauptfenster `TFormAndiGeneratorMain` und startet die Nachrichtenschleife. Fachliche Logik gibt es hier nicht. Der große Kommentarblock ist aber als Quelle für Anforderungen und Performance-Referenzwerte wichtig.

## Inhalt / Struktur
- `uses`: `FastMM4` wird bedingt eingebunden (`{$ifndef DEBUG_MULTITHREAD}`). In der Debug-Konfiguration ist `DEBUG_MULTITHREAD_NO` definiert, also **nicht** `DEBUG_MULTITHREAD`. Deshalb ist FastMM4 praktisch immer aktiv.
- `{$R *.res}`: Ressourcen (Icon, Versionsinfo, Manifest).
- Hauptblock (Z. 92–97):
  ```
  Application.Initialize;
  Application.MainFormOnTaskbar := True;
  Application.CreateForm(TFormAndiGeneratorMain, FormAndiGeneratorMain);
  Application.Run;
  ```
- **TODO-Liste (Z. 19–72)**, wörtlich zusammengefasst. Sie zeigt geplante, aber (teilweise) nicht umgesetzte Features:
  - Warnung beim Export bei verletzten Sperrterminen, der **40-km-Regel**, Spielen mit weniger als 3 Tagen Abstand und vereinsinternen Spielen nicht am Anfang
  - Sperrtermine abhängig von der Zahl der Wunschtermine höher gewichten
  - gewünschte Koppeltermine bevorzugen; gemeinsamen letzten Spieltag verbessern (eigene Kostenart)
  - neue Kostenart „lange Spielpausen“ (bzw. „Lange Spielpause ersetzen durch Spielverteilung“)
  - Spielreihenfolge nach erwarteter Platzierung (die Stärksten und Schwächsten spielen am Ende gegeneinander) → heute Kostenart `mktRanking`
  - Kostengewichtung pro Mannschaft einstellbar
  - „alle übertragenen Daten von Click-TT editierbar“: Mannschaften, Terminmeldungen, 60-km-Mannschaften, Sperrtermine, Ausweichtermine, vordefinierte Spiele, parallele Spiele, Auswärtskoppelmannschaften und -ersatztermine, Spiellokale (Standardmannschaft, Wunschtermine, Nachbarmannschaften und deren Spiele) → heute weitgehend über `DialogForMultiplePanel` umgesetzt
  - AutomaticTaborder (→ `PlanUtils.FixControls`), „Neu erstellen ohne Click-TT“ (→ `MenuNew`), Updatecheck (→ `DialogGetUpdates`), Heimrecht (→ `DialogPanelHomeRight`)
  - Spiellokale und Koppeltermine in eigenen Dialogen; freie Eingabe paralleler bzw. nicht paralleler Mannschaften; Anzeige paralleler Spiele; Setzliste; Handplan; Pflichtspieltage am Anfang abfragen; Fehlermeldung bei Doppelspieltagen; breiterer Gewichtungseditor; „Exception bei Testplänen im Programmverzeichnis“; Startwarnung für Ausweichtermine; „Manuelle Termine werden erst übernommen, wenn eine Verbesserung gefunden wurde“; Rundennummer-CSV bei Doppelrunden
  - „Manuelle Termine nicht in den Kosten berücksichtigen (nur Hinweis)“
  - „Performanceverbesserung bei Auswärtskoppelterminen (1 Thread mit höherer Gewichtung mitlaufen lassen)“ → umgesetzt als **SpecialThread** in `PlanOptimizer`
  - „Generierung der 20 ‚besten Pläne‘“
- **Performance-Historie (Z. 75–87)**, jeweils gemessen „nach 1 Mio Plänen“:

  | Datum | 2. Kreisliga | 1. Kreisliga |
  |---|---|---|
  | 20.09.15 | 5.200 /s | 8.200 /s |
  | 21.09.15 | 18.000 /s | 14.500 /s |
  | 11.10.15 | 40.000 /s | 32.000 /s |
  | 21.10.15 | 36.000 /s | 28.000 /s |

  Das sind die Referenzwerte für die C#-Performance (Pläne/s = Summe aller Worker-Iterationen, siehe `TPlanOptimizer.PlanPerSec`). Hardware und Thread-Zahl sind nicht dokumentiert.

### Relevante Build-Einstellungen aus AndiGenerator.dproj
- Basis-Konfiguration (für alle Builds): `DCC_Define = PROFILE_1;SPECIALTHREAD` → **der SpecialThread ist in allen Builds aktiv**. `PROFILE` ist *nicht* definiert (nur `PROFILE_1`), deshalb bleibt der Profil-Button unsichtbar.
- Debug (`Cfg_1`): `DEBUG;TRACE;DEBUG_MULTITHREAD_NO`, Optimierung aus. Debug Win32 zusätzlich `CACHE_HIT_TEST;CACHE_TEST`.
- Versionsinfo: FileVersion 1.0.6.0 (Basis), 1.0.9.0 (Win32), Debug-Win32 22.6.2.0. Locale 1031/1033. CompanyName „Andreas Hofmann“, FileDescription „Andigenerator, Termingenerator für Tischtennisstaffeln“.
- Plattformen Win32 und Win64 mit eigenen Manifesten (`andigenerator32.manifest`, `andigenerator64.manifest`) und Icons (`AndiGenerator_Icon1/2/3.ico`).

## Fachliche Logik / Regeln
Hier ist keine Fachlogik implementiert. Die TODO-Liste nennt eine **40-km-Regel** (im Code heißt die Kostenart `mkt60Kilometer` bzw. „60km“). Das ist vermutlich eine veraltete Bezeichnung.

## Daten & Persistenz
Keine.

## Threading / Performance
Keine eigene Nebenläufigkeit. FastMM4 (siehe `FastMM4Options.inc`: `AssumeMultiThreaded`, `NeverSleepOnThreadContention`, `UseSwitchToThread`) war für die Allokations-Performance der vielen Worker-Threads wichtig. In C# übernimmt das der .NET-GC. Die Allokationen im Hot-Path müssen dort trotzdem minimiert werden.

## Plattformabhängigkeiten
- VCL-`Application`, `MainFormOnTaskbar`, Windows-Ressourcen (.res, Manifest, Icon)
- FastMM4 (x86/x64-Assembler, nur Windows)

## Migrationshinweise für C#
- Ziel: `AndiGenerator.App` (z.B. Avalonia UI für Windows/macOS/Linux, optional .NET MAUI für Mobilgeräte), `Program.cs` mit `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`.
- Bedingte Kompilierung: `SPECIALTHREAD` am besten als Laufzeit-Option (Standard: an), `TRACE` als Logging-Level (`Microsoft.Extensions.Logging`), `PROFILE` bzw. `CACHE_HIT_TEST` als Diagnose-Schalter.
- FastMM4 fällt weg. Für den Server-GC bzw. Concurrent GC `<ServerGarbageCollector>` oder `<ConcurrentGarbageCollection>` prüfen, dazu `TieredPGO` und ReadyToRun bzw. NativeAOT (NativeAOT funktioniert nicht, wenn Reflection-lastige UI-Bibliotheken verwendet werden).
- Versionsinfo: `<Version>` in der .csproj. Das Hauptfenster zeigt die Version im Titel an (`GetFileVersionString`) → `Assembly.GetName().Version`.
- Die TODO-Liste gehört ins Backlog und nicht in den Code.
- Aufwand: **S**, denn es handelt sich nur um den Bootstrap.

## Offene Fragen
- Soll die TODO-Liste für die C#-Version als Feature-Backlog übernommen werden, und welche Punkte sind heute schon erledigt?
- Auf welcher Hardware und mit welcher Thread-Zahl entstanden die Referenzwerte (bis 40.000 Pläne/s)? Welcher Zielwert gilt für die C#-Version?
- Ist mit „40-km-Regel“ dieselbe Regel gemeint wie `mkt60Kilometer`?
