# PlanOptimizer (PlanOptimizer.pas)

**Kategorie:** Optimierung/Threading
**Umfang:** 517 Zeilen .pas / keine .dfm
**Abhängigkeiten (uses):** Projekt: `PlanTypes` (`TPlan`, `cMaxThreads`, `TMannschaftsKostenType`, `cMannschaftsKostenTypeNamen`), `PlanUtils` (`MyDouble`, `MyFormatFloat`, `TraceString`), `PlanMainThread` (`TPlanMainThread`). RTL: `TThread`, `SyncObjs.TCriticalSection`, `Windows.GetTickCount`, `TList`.
**Verwendet von:** `AndiGeneratorMain` (erzeugt, startet, pausiert und beendet den Optimierer, holt Ergebnisse per `copyPlanDates`, `setNewPlanData`, `Durchlaufe`, `PlanPerSec`).

## Zweck
`TPlanOptimizer` ist die **oberste Ebene** der dreistufigen Optimierer-Hierarchie und die einzige Schnittstelle zur UI. Er startet `cMaxThreads = 4` Inseln (`TPlanMainThread`). Etwa einmal pro Sekunde übernimmt er den global besten Inselplan in seinen eigenen `Plan` und startet die **schlechteste Insel** neu, wenn diese zu lange keine Verbesserung gefunden hat. Das ist eine Diversifikations- und Restart-Strategie. Optional startet er einen **SpecialThread**, eine fünfte Insel, in der jeweils eine Mannschafts-Kostenart extrem hoch gewichtet ist. Deren Ergebnis dient als Startpunkt für die nächste neu zu startende Insel. Außerdem verteilt er neue Plandaten und Optionen aus der UI an alle Inseln.

## Inhalt / Struktur

### Konstanten (abhängig von `{$ifopt D+}`, also Debug-Info an oder aus)
| Konstante | Debug (D+) | Release | Bedeutung |
|---|---|---|---|
| `cDurchLaufBeforeReinit` | 1.000 | **100.000** | Iterationen der schlechtesten Insel ohne Verbesserung, bevor sie neu gestartet wird |
| `cDurchLaufBeforeStartSpecialThread` | 300.000 | **3.000.000** | Gesamtiterationen (alle Inseln), ab denen der SpecialThread gestartet wird |
| `cMinDurchLaufSpecialThread` | 20.000 | **200.000** | Mindestiterationen des SpecialThreads, bevor sein Plan zum Neustart einer Insel verwendet wird |

Aus PlanTypes: `cMaxThreads = 4`, `NUM_DYN_THREADS = 9`, 14 feste Strategien (siehe PlanMainThread.md). Eine auskommentierte Ein-Thread-Variante (`cMaxThreads = 1`, nur `'R'`) bricht im Release per `{$Message Fatal}` ab.

### Felder
| Feld | Bedeutung |
|---|---|
| `Plan: TPlan` | global bester Plan (von der UI per `copyPlanDates` gelesen) |
| `CriticalSectionOptimizer` | Optimierer-Lock (= `ParentCriticalSection` der Inseln) |
| `Threads: TList` | die 4 Inseln |
| `SpecialThread: TPlanMainThread` | optionale 5. Insel |
| `SpecialThreadMaxGewichtung` | aktuell extrem gewichtete Kostenart (Start: `mktAbstandHeimAuswaerts`) |
| `Durchlaufe` | Summe aller Iterationen (Inseln + Special), vom UI-Thread **ohne Lock** gelesen |
| `LastResetDurchlauf`, `StartTick` | Basis für `PlanPerSec` |
| `ReInitPlanData: boolean` | Flag „neue Plandaten verteilen“ |
| `FPaused` | Pausenzustand |

### Methoden
- `Create`: suspendiert, legt Plan, Liste und Lock an.
- `Execute`:
  1. `StartThreads`: unter dem Optimierer-Lock 4 Inseln `TPlanMainThread` mit den Namen „1“..„4“ und `InitWithPlan(Plan)`, pausiert gestartet. `SpecialThread := nil`. Danach `WaitForThreadInit` und `SetPaused(FPaused)`.
  2. Schleife bis `Terminated`:
     - `Paused` → `Sleep(100)`.
     - Sind seit der letzten Messung > 1000 ms vergangen (effektiv **≈ 1,1 s**):
       - Optimierer-Lock nehmen. `ActPlanKosten := Plan.CalculateKosten(-1)`.
       - Für jede Insel `GetActResult(nil, …)`: Iterationen aufsummieren. **Schlechteste Insel** bestimmen = höchste Kosten ≥ 0 (`BadestThread`, `BadestKosten`, `BadestThreadDurchLaufe := DurchLaufOhneVerbesserung`). Hat die Insel bessere Kosten als `ActPlanKosten` (und kein ReInitPlanData steht an), wird ihr Plan in `Self.Plan` übernommen (`GetActResult(Plan, …)`). Die Inseln tauschen **untereinander keine Lösungen** aus. Nur der Optimierer sammelt das globale Optimum.
       - `Durchlaufe` = Summe + Iterationen des SpecialThreads.
       - Ist `Durchlaufe > cDurchLaufBeforeStartSpecialThread`, wird `StartSpecialThread()` aufgerufen (wirkt nur einmal. Ohne `SPECIALTHREAD` bleibt der SpecialThread nil).
       - **Re-Init-Strategie**: Ist `BadestThreadDurchLaufe > cDurchLaufBeforeReinit` und kein ReInitPlanData steht an und `cMaxThreads > 1`:
         - `ReinitString := ' Reinit: ' + <Durchläufe in Mio>`.
         - Existiert der SpecialThread und gilt `SpecialThread.Durchlaufe > cMinDurchLaufSpecialThread`:
           - Plan des SpecialThreads in `TempPlan` (Kopie des Optimierer-Plans, also **normale Gewichtung**, mit den Terminen des Special-Plans) holen, dann `BadestThread.ReInit(TempPlan, 'Von Spezial<Kostenart>')` → die schlechteste Insel startet mit der Special-Lösung, bewertet aber mit normalen Gewichten.
           - `SpecialThread.Durchlaufe := 0` (wirkungslos, siehe Bugs), neue Kostenart per `getNextMaxGewichtung(Plan)`, `SpecialThread.ReInit(Plan, '')` (Special übernimmt den global besten Plan) und `SpecialThread.SetMaxOption(neue Kostenart)`.
         - Sonst: `BadestThread.ReInitAll(ReinitString)` → **die schlechteste Insel beginnt komplett von vorn** (alle Termine gelöscht).
         - Der Zähler „ohne Verbesserung“ der Insel wird durch den Re-Init zurückgesetzt. Eine erneute Auslösung für dieselbe Insel erfolgt erst nach weiteren 100.000 Iterationen ohne Verbesserung.
         - Es wird nur **die** schlechteste Insel betrachtet. Stagniert eine mittlere Insel, während die schlechteste sich noch verbessert, passiert nichts.
       - **ReInitPlanData** (neue Plandaten aus der UI): Lock nochmals nehmen (reentrant), für alle Inseln `ReInit(Plan, Insel.ReinitString)`, für den SpecialThread `ReInit(Plan, '')`. Flag löschen, `StartTick` und `LastResetDurchlauf` für die Pläne/s-Messung zurücksetzen.
       - Lock freigeben.
     - `Sleep(100)`.
  3. `finally KillThreads` (Inseln und SpecialThread: `Terminate`, Busy-Wait `Sleep(2)`, `Free`).
- `getNextMaxGewichtung(Plan)`: Kandidatenmenge = die Kostenarten, die im aktuellen Plan **Kosten > 0** verursachen und fachlich vorhanden sind:
  - `mktAuswaertsKoppelTermine` (falls `HasAuswaertsKoppelTermine`), `mktKoppelTermine` (falls `HasKoppelTermine`), `mktRanking` (falls `HasRanking`), `mktSisterGames`, `mkt60Kilometer` (falls `Has60KilometerValues`), `mktAbstandHeimAuswaerts`, `mkt2SpieleProWoche`.
  - Zufallsauswahl per Rejection-Sampling: `Random(Ord(High(TMannschaftsKostenType)))` liefert 0..14, `mktMandatoryGames` (15) wäre also nie wählbar, ist aber auch kein Kandidat. Bei leerer Menge: `mktAbstandHeimAuswaerts`.
- `StartSpecialThread` (nur `{$ifdef SPECIALTHREAD}`, laut .dproj in **allen** Builds definiert): neue Insel „Special“ mit `InitWithPlan(Plan, CriticalSectionOptimizer)`, Kostenart wählen, `ReInit(Plan,'')` + `SetMaxOption(Kostenart)` (setzt in der Kopie `GewichtungMainMannschaftsKosten[k] := coExtremHoch`). Start pausiert, dann `Paused := FPaused`, Busy-Wait bis `IsInit`. **Achtung:** wird innerhalb des gehaltenen Optimierer-Locks aus `Execute` aufgerufen und wartet dort, bis 24 Worker initialisiert sind.
- `SetPaused(v)`: an alle Inseln und den SpecialThread weitergeben, `StartTick`/`LastResetDurchlauf` zurücksetzen (Pläne/s wird nach der Pause neu gemessen).
- `copyPlanDates(Plan)`: unter dem Lock `Plan.AssignOptimizedDates(Self.Plan)` (von der UI aufgerufen).
- `setNewPlanData(Plan)`: unter dem Lock `Self.Plan.AssignPlanData(Plan)`, `RemoveNotValidDates`, `ReInitPlanData := true` (die Verteilung erfolgt im nächsten Poll).
- `PlanPerSec`: `(Durchlaufe − LastResetDurchlauf) / ((GetTickCount − StartTick)/1000)`. Das ist ein Durchschnitt seit Start, Pause oder Datenänderung, keine Momentanrate.
- `initialization Randomize()`.

## Fachliche Logik / Regeln
- Keine Spielplanregeln. Die **Suchstrategie** besteht aus:
  1. Insel-Modell mit 4 unabhängigen Populationen (je 24 Worker mit verschiedenen Nachbarschaftsoperatoren).
  2. Globales Optimum = beste Insel.
  3. **Restart der schlechtesten Insel** nach 100.000 Iterationen ohne Verbesserung (Release).
  4. Ab 3 Mio Gesamtiterationen eine **Spezial-Insel** mit extrem gewichteter, zufällig gewählter „problematischer“ Kostenart. Deren Ergebnis dient als Startpunkt für die neu gestartete Insel (bei normaler Gewichtung). Das entspricht der TODO-Notiz „Performanceverbesserung bei Auswärtskoppelterminen (1 Thread mit höherer Gewichtung mitlaufen lassen)“.
- Kostenarten (`TMannschaftsKostenType`, PlanTypes): `mktHalleBelegt, mktSisterGames, mktForceSameHomeGames, mktSperrTermine, mktAusweichTermine, mkt60Kilometer, mktEngeTermine, mkt2SpieleProWoche, mktSpielverteilung, mktKoppelTermine, mktAuswaertsKoppelTermine, mktZahlHeimSpielTermine, mktWechselHeimAuswaerts, mktAbstandHeimAuswaerts, mktRanking, mktMandatoryGames`.

## Daten & Persistenz
Keine.

## Threading / Performance
- **Polling-Intervalle** (Übersicht über alle Ebenen):

  | Ebene | Takt | Pause |
  |---|---|---|
  | UI-Timer (`TimerSyncPlans`) | 777 ms | Timer läuft weiter, ignoriert pausierten Optimierer |
  | Optimierer | > 1000 ms + Sleep 100 ≈ 1,1 s | Sleep(100) |
  | Insel | > 100 ms + Sleep 100 ≈ 0,2 s | Sleep(100) |
  | Worker | Endlosschleife, `Sleep(0)` je Iteration; LowPrio-Worker nach 10.000 Iterationen 50 ms Sleep | Sleep(100) |

- **Thread-Zahl fest**: 4 Inseln × 24 = 96 Worker, ab 3 Mio Iterationen +24 (Special) = **120 Worker**, unabhängig von `CPU-Kernen`. Die Worker haben `tpLower`. Auf einem 4-Kerner teilen sich also ca. 30 Worker einen Kern. Das kostet Kontextwechsel und Cache-Misses (jeder Worker hat 2 eigene `TPlan`-Kopien).
- **Locks**: Optimierer-Lock → Insel-Lock (`GetActResult`), niemals umgekehrt. Der UI-Thread nimmt den Optimierer-Lock (`copyPlanDates`, `setNewPlanData`). Da der Optimierer den Lock für die gesamte Ernte hält (inkl. Warten auf die Insel-Locks, die ihrerseits während der Insel-Ernte blockiert sind), kann die UI kurz blockieren. Besonders lang ist die Blockade in `StartSpecialThread` (Busy-Wait auf 24 Worker-Initialisierungen unter dem Lock).
- `Durchlaufe` (Int64) wird vom UI-Thread ohne Lock gelesen. Auf Win32 ist das nicht atomar (nur Anzeige).

## Plattformabhängigkeiten
- `GetTickCount` (32 Bit, Überlauf nach 49,7 Tagen. `PlanPerSec` rechnet mit DWORD-Differenz, das ist unkritisch).
- Ansonsten `TThread`/`TCriticalSection`, gut portierbar.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.Engine.Optimizer` (public API für UI bzw. ViewModel):
  ```csharp
  Task StartAsync(PlanSnapshot start, CancellationToken ct);
  bool Paused { get; set; }
  PlanSolution GetBest();                 // statt copyPlanDates, lock-frei per Volatile.Read
  void UpdatePlanData(PlanData newData);  // statt setNewPlanData
  long Iterations { get; }                // Interlocked
  double PlansPerSecond { get; }
  event EventHandler<ImprovementEventArgs> Improved; // optional, statt UI-Polling
  Task StopAsync();
  ```
- **Parallelität**: Die Worker-Zahl richtet sich nach `Environment.ProcessorCount` (konfigurierbar). Die Inselzahl wird z.B. `max(2, cores/6)` oder bleibt fest 4. Strategien werden den Workern zyklisch zugeordnet. So nutzt die C#-Version alle Kerne, ohne 120 Threads anzulegen. Dedizierte `Thread`s (nicht der ThreadPool, weil die Worker endlos und CPU-bound laufen) mit `ThreadPriority.BelowNormal`. Auf Mobilgeräten die Kernzahl bzw. den Energiesparmodus berücksichtigen.
- SpecialThread: als Konfigurationsoption beibehalten (Standard an, wie im Original).
- Konstanten: Release-Werte übernehmen, in eine `OptimizerSettings`-Klasse auslagern. Sie beziehen sich auf die **Summe der Worker-Iterationen einer Insel**. Ändert sich die Worker-Zahl pro Insel, ändern sich die Zeitkonstanten effektiv mit (ggf. pro Worker normieren oder zeitbasiert machen).
- Re-Init-Aufträge über ein `ReinitRequest`-Objekt pro Insel (atomar ersetzt) statt Flag, Datenfeld und String getrennt. Damit sind die Race-Bugs beseitigt.
- `getNextMaxGewichtung`: Rejection-Sampling durch `candidates[rng.Next(candidates.Count)]` ersetzen (gleiche Verteilung, deterministisch terminierend).
- Zufall: pro Worker eigene RNG-Instanz. Für `getNextMaxGewichtung` die RNG des Optimierers.
- `PlanPerSec`: `Stopwatch` statt `GetTickCount`.
- Aufwand: **M**. Der Code ist klein, aber das Zusammenspiel mit Insel, Worker und UI muss neu und sauber nebenläufig entworfen werden. Performance-Messungen gegen die Referenzwerte aus AndiGenerator.dpr sind nötig.

### Bugs/Auffälligkeiten
- **Z. 185, `SpecialThread.Durchlaufe := 0` ist wirkungslos**: `TPlanMainThread.Execute` überschreibt `Durchlaufe` bei jedem Poll (≈200 ms) mit der Summe der **kumulativen** Worker-Zähler (die nie zurückgesetzt werden). Deshalb gilt die Bedingung `SpecialThread.Durchlaufe > cMinDurchLaufSpecialThread` nach dem ersten Erreichen praktisch dauerhaft. **Jeder** weitere Restart der schlechtesten Insel verwendet die Special-Lösung (nach teils nur ca. 1 s Special-Laufzeit mit der neuen Gewichtung) statt eines kompletten Neustarts. `ReInitAll` (Neustart von leer) kommt dann nicht mehr vor. Außerdem ist der Schreibzugriff ohne Lock ein Datenrennen auf einen Int64. Für die Migration entscheiden: Absicht umsetzen (Zähler seit dem Special-Re-Init, also `DurchLaufAfterReInit` verwenden) oder das Ist-Verhalten beibehalten.
- **Z. 187–188, Race `ReInit` → `SetMaxOption`**: zwei getrennte Lock-Abschnitte. Verarbeitet die Special-Insel dazwischen den Re-Init, setzt `SetMaxOption` erneut `FlagReInitOptions` ohne `ReInitPlanData` → `AssignPlanData(nil)` → **Access Violation** im Insel-Thread (siehe PlanMainThread.md). Außerdem läuft die Special-Insel dann ohne Extremgewichtung weiter.
- Z. 218: Verschachteltes `CriticalSectionOptimizer.Enter` (reentrant, funktioniert. In C# mit `lock` ebenfalls, mit `SemaphoreSlim` würde es deadlocken).
- Z. 230 (nur TRACE): `BadestThread.ThreadName`. `BadestThread` kann nil sein (wenn alle Inseln Kosten −1 melden, was direkt nach einem Re-Init typisch ist) → Access Violation im Trace-Build.
- Z. 152 bzw. 399–421: `StartSpecialThread` wird unter dem gehaltenen Optimierer-Lock aufgerufen und wartet per Busy-Wait auf die Initialisierung der 24 Worker. Die UI (`copyPlanDates`) blockiert so lange.
- Z. 144–148: Die Kosten des SpecialThreads fließen nicht in die Wahl des globalen Optimums ein (richtig, weil andere Gewichtung). Seine Iterationen zählen aber in `Durchlaufe` und `Pläne/s`.
- Z. 301: `Random(Ord(High(...)))` schließt den letzten Enum-Wert aus (hier ohne Folgen). Das globale `Random` wird ohne Synchronisation mit den Worker-Threads geteilt.
- Z. 120–125: Bei Gleichstand der Kosten gewinnt die erste Insel. Eine Insel, die gerade einen Re-Init hat (Kosten −1), wird nie als schlechteste gewählt, das ist korrekt.

## Offene Fragen
- Soll die feste Thread-Topologie (4 Inseln × 24 Worker + Special) übernommen oder an die Kernzahl angepasst werden? Welcher Pläne/s-Zielwert gilt?
- Sollen die Bugs „SpecialThread-Zähler-Reset wirkungslos“ und „DynamicThreads[0]“ korrigiert werden? Beides ändert das Suchverhalten. Idealerweise mit Benchmark-Ligen vergleichen (1. und 2. Kreisliga aus der Performance-Historie).
- Soll der SpecialThread in der C#-Version konfigurierbar bzw. abschaltbar sein?
- Soll der Suchlauf reproduzierbar sein (fester Seed, deterministische Worker-Zuordnung)? Das ist mit Multi-Threading nur eingeschränkt möglich.
