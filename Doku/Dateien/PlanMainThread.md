# PlanMainThread (PlanMainThread.pas)

**Kategorie:** Optimierung/Threading
**Umfang:** 441 Zeilen .pas / keine .dfm
**Abhängigkeiten (uses):** Projekt: `PlanTypes` (`TPlan`, `TPlanCalcThread`, `cThreadTauschPercent`, `NUM_DYN_THREADS`, `TMannschaftsKostenType`, `coExtremHoch`), `PlanUtils` (`MyDouble`, `TraceString`), `DialogProfile` (`AddThreadTypeToStatistics`, `AddThreadInfoToStatistics`, nur mit `{$ifdef PROFILE}`). RTL: `Classes.TThread`, `SyncObjs.TCriticalSection`, `Windows.GetTickCount`, `TList` (untypisiert).
**Verwendet von:** `PlanOptimizer`.

## Zweck
`TPlanMainThread` ist die **mittlere Ebene („Insel“)** der dreistufigen Optimierer-Hierarchie. Jede Insel besitzt einen eigenen Plan und startet 14 feste plus 10 dynamische Worker-Threads (`TPlanCalcThread`, definiert in PlanTypes). Im Abstand von ca. 200 ms sammelt die Insel das beste Worker-Ergebnis ein und übernimmt es in den Inselplan. Danach setzt sie **alle** Worker auf diesen besten Plan zurück (synchrone „Best-of“-Verteilung). Zusätzlich passt sie die Strategie der dynamischen Worker an die zuletzt erfolgreiche an. Die Insel führt außerdem Re-Inits aus, die der Optimierer anfordert (komplett neu starten oder neue Plandaten/Optionen übernehmen).

## Inhalt / Struktur

### Thread-Hierarchie (Gesamtbild, Konstanten aus PlanTypes)
```
TPlanOptimizer (1 Thread, Poll ≈1,1 s)
 ├─ TPlanMainThread "1".."4"   (cMaxThreads = 4, Poll ≈0,2 s)
 │    ├─ 14 × TPlanCalcThread fest   (cThreadTauschPercent[0..13])
 │    └─ 10 × TPlanCalcThread "|Dyn" (for i := 0 to NUM_DYN_THREADS=9, Starttyp '2')
 └─ SpecialThread: TPlanMainThread "Special" (nur {$ifdef SPECIALTHREAD}, 1 Insel mit ebenfalls 24 Workern)
```
- Worker-Strategien `cThreadTauschPercent = ('R','100','15','5','2','1','S1,100','S1,25','S1,10','S2,10','M1,10','M1,25','M1,100','M2,10')`:
  - `R` = Raster (`ttRaster`, Round-Robin-Neuaufbau), LowPrio
  - Zahl p = `ttNormal`, p % der Spiele neu würfeln; LowPrio bei p ≥ 30 (also `100`)
  - `S<n>,<p>` = `ttSpielTag`, n Spieltage, p %
  - `M<n>,<p>` = `ttMannschaft`, n Mannschaften, p %
  - Parsing in `TPlanCalcThread.SetThreadType`: `TauschArt[2]` ist **eine** Ziffer, der Prozentwert ab Position 4.
- Summe: 4 × 24 = **96 Worker** + 1 SpecialThread-Insel mit 24 Workern = **120 Worker-Threads** + 5 Insel-Threads + 1 Optimizer + UI. Die Zahl ist **fest** und hängt **nicht** von der Zahl der CPU-Kerne ab. Worker laufen mit `Priority := tpLower`.
- Worker-Schleife (PlanTypes, zum Verständnis): `TransferRefDateToDate → FillPredefinedGames → NeuWuerfeln(TauschTyp, Anzahl, Percent) → FillTermine → CalculateKosten(OptKosten)` (mit Early-Exit ab der bisherigen Bestmarke). Bei einer Verbesserung: Insel-Lock nehmen, Kosten voll neu berechnen, `TransferDateToRefDate`, `OptPlan.AssignOptimizedDates(Plan)`. Nach jeder Iteration `Sleep(0)`. LowPrio-Worker (`R`, ≥ 30 %) schlafen nach 10.000 Iterationen seit dem letzten Re-Init je Iteration 50 ms.

### Felder von `TPlanMainThread`
| Feld | Bedeutung |
|---|---|
| `Plan: TPlan` | bester Plan der Insel |
| `RetKosten: MyDouble` | Kosten von `Plan` (−1 = unbekannt) |
| `Durchlaufe: Int64` | Summe der (kumulativen, nie zurückgesetzten) Worker-Iterationen, bei jedem Poll neu berechnet |
| `ReInitByDurchLauf`, `VerbesserungByDurchLauf` | `Durchlaufe`-Stand beim letzten Re-Init bzw. bei der letzten Verbesserung |
| `ReinitString` | Info-Text für Thread-Namen bzw. Trace |
| `ReInitPlanData: TPlan` | angeforderte neue Plandaten (Optionen und Daten) |
| `FlagReInitAllDates`, `FlagReInitOptions` | Re-Init-Anforderungen des Optimierers |
| `Threads`, `DynamicThreads: TList` | alle Worker bzw. Teilmenge der dynamischen Worker (Reihenfolge = LRU) |
| `CriticalSectionMainThread` | Insel-Lock. Wird **auch den Workern als deren `ParentCriticalSection`** übergeben. |
| `ParentCriticalSection` | Lock des Optimierers |
| `ThreadName`, `IsInit`, `FPaused` | |

### Methoden
- `Create`: suspendiert (`inherited Create(true)`), legt Lock, Plan und Listen an.
- `InitWithPlan(Name, Plan, ParentCS)`: kopiert den Plan (unter dem Insel-Lock).
- `Execute`:
  1. `StartThreads`, `IsInit := True`.
  2. Schleife bis `Terminated`:
     - `Paused` → `Sleep(100)`, weiter.
     - Sind seit der letzten Messung > 100 ms vergangen:
       - `RetKosten := Plan.CalculateKosten(-1)` (**außerhalb** des Locks).
       - Insel-Lock nehmen.
       - **FlagReInitAllDates**: `RetKosten := −1`, beide Zähler auf `Durchlaufe` setzen, `Plan.clearAllDates()` und alle Worker `DoReInitPlan(Plan, ReinitString)` → alle Worker starten mit leerem Terminplan neu (Neuanfang der Insel).
       - **FlagReInitOptions**: Zähler zurücksetzen, `Plan.AssignPlanData(ReInitPlanData)` (die Termine des Inselplans bleiben erhalten, weil `AssignPlanData` die Schedule sichert und wiederherstellt), `RemoveNotValidDates`, `FreeAndNil(ReInitPlanData)`, Kosten neu berechnen, alle Worker `DoReInitPlanData(Plan, ReinitString)`.
       - Ernte: `Durchlaufe := Σ Worker.Durchlauf`. Für jeden Worker `GetActResult(nil)` → Sind seine Kosten ≥ 0 und kleiner als `RetKosten` (oder `RetKosten < 0`), wird `TempPlan := Kopie(Plan)` angelegt, der Worker-Plan per `GetActResult(TempPlan)` abgeholt und `Plan.AssignOptimizedDates(TempPlan)` aufgerufen. Außerdem `RetKosten := Kosten`, `MustReinit := True`, `ThreadBestType := Worker.ThreadType`. Es wird über **alle** Worker iteriert, sodass am Ende der beste übernommen ist. Die Plausibilitätsprüfungen `|Kosten − CalculateKosten| > 1` erzeugen nur einen Trace („Hier ist was oberfaul“).
       - Bei **MustReinit** (Verbesserung gefunden): `VerbesserungByDurchLauf := Durchlaufe`, alle Worker `DoReInitPlan(Plan, '')` → alle machen mit dem besten Plan weiter. **Dynamische Anpassung**: Die Zahl der dynamischen Worker mit dem Typ `ThreadBestType` wird gezählt (fehlerhaft, siehe Bugs). Ist sie `< DynamicThreads.Count div 3` (= 10 div 3 = 3), wird `DynamicThreads[0]` (der am längsten nicht umgestellte) auf `ThreadBestType` umgestellt (`SetThreadType`) und ans Listenende verschoben. Mit PROFILE werden Statistiken an `DialogProfile` gemeldet.
       - Lock freigeben, `LastMessungTick` setzen.
     - `Sleep(100)`.
  3. `finally KillThreads`.
  - **Effektives Poll-Intervall** ≈ 100 ms Bedingung + 100 ms Sleep ≈ **200 ms** (Auflösung von GetTickCount ca. 16 ms).
- `StartThreads`: unter dem Insel-Lock 14 feste Worker (`StartWithLists(ThreadName, Plan, Typ, CriticalSectionMainThread)`, `Paused := True`, `Start`) und 10 dynamische (`ThreadName + '|Dyn'`, Typ `'2'`). Danach `WaitForThreadInit` (Polling `Sleep(10)` bis alle `IsInit`), dann `SetPaused(FPaused)`.
- `KillThreads`: pro Worker `Terminate` und Busy-Wait `Sleep(2)` bis `Finished`, dann `Free`.
- `SetPaused(v)`: setzt `FPaused` und `Paused` aller Worker (ohne Lock, reine Bool-Flags).
- `GetActResult(Plan, var Durchlauf, DurchLaufAfterReInit, DurchLaufOhneVerbesserung, Kosten)`: nimmt **erst `ParentCriticalSection` (Optimierer), dann den Insel-Lock**. Kopiert optional die Termine (`Plan.AssignOptimizedDates(Self.Plan)`), liefert `Durchlaufe`, `Durchlaufe − ReInitByDurchLauf`, `Durchlaufe − VerbesserungByDurchLauf` und `RetKosten`. Steht ein Re-Init an, ist `Kosten = −1`.
- `ReInitAll(ReinitString)`: setzt `FlagReInitAllDates := True` und `ReinitString` (**ohne Lock**).
- `ReInit(Plan, ReinitString)`: unter dem Insel-Lock `FlagReInitOptions := True`, `ReInitPlanData := Kopie(Plan)`.
- `SetMaxOption(KostenType)`: unter dem Insel-Lock `FlagReInitOptions := True`. Nur wenn `ReInitPlanData` existiert, wird dort `GewichtungMainMannschaftsKosten[KostenType] := coExtremHoch` gesetzt (für den SpecialThread).
- `initialization Randomize()`.

## Fachliche Logik / Regeln
Keine Tischtennis-Regeln. Die Unit enthält die Metaheuristik: eine Insel mit synchroner Elitenübernahme (alle Worker auf die beste Lösung zurücksetzen) und adaptiver Strategieauswahl (dynamische Worker übernehmen die zuletzt erfolgreiche Tauschstrategie).

## Daten & Persistenz
Keine.

## Threading / Performance
- **Locks**:
  - Worker verwenden den Insel-Lock (`CriticalSectionMainThread`) für: Verbesserung melden, Re-Init übernehmen, `GetActResult`, `DoReInit*`. Alle 24 Worker einer Insel serialisieren sich bei Verbesserungen auf diesem Lock.
  - Die Insel hält den Lock während der gesamten Ernte (inkl. `TPlan.Create/Assign`, mehrfach `CalculateKosten(-1)`, `AssignOptimizedDates`, `DoReInitPlan` für 24 Worker mit jeweils einer vollen `TPlan`-Kopie). In dieser Zeit blockieren alle Worker, die eine Verbesserung melden wollen.
  - Lock-Reihenfolge: Optimierer-Lock → Insel-Lock (`GetActResult`). Die Insel nimmt den Optimierer-Lock nie, deshalb gibt es keinen Deadlock. `TCriticalSection` ist reentrant, das wird genutzt (Insel hält den eigenen Lock und ruft `Worker.GetActResult` auf, das denselben Lock erneut nimmt).
- **Allokationen im Poll**: Bei jeder Verbesserung entstehen eine `TPlan`-Kopie für TempPlan und 24 `TPlan`-Kopien in `DoReInitPlan`. Das sind teure tiefe Kopien, die aber nur ca. 5×/s und nur bei Verbesserungen anfallen.
- **Busy-Waits**: `WaitForThreadInit` (10 ms), `KillThreads` (2 ms).
- **Pause**: kooperativ. Alle Ebenen prüfen das Flag und schlafen 100 ms.
- **Zufall**: Das globale Delphi-`Random` wird aus allen Worker-Threads verwendet (nicht threadsicher, aber in Delphi ohne Absturz. Die Folge sind korrelierte bzw. „zerkratzte“ Zufallszahlen). `Randomize` in `initialization`.

## Plattformabhängigkeiten
- `GetTickCount` (32-Bit-ms, Überlauf nach 49,7 Tagen. Die Differenzbildung mit DWORD funktioniert dank unsigned-Arithmetik trotzdem).
- `TThread.Priority := tpLower` (Windows-Thread-Priorität).
- Ansonsten nur RTL (`TThread`, `TCriticalSection`), das ist gut portierbar.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.Engine.Island` (statt Thread eine Klasse mit `Task RunAsync(CancellationToken)`), Worker als `AndiGenerator.Engine.Worker`.
- **Thread-Anzahl dynamisch**: `Environment.ProcessorCount` verwenden. Statt 120 OS-Threads auf z.B. 8 Kernen besser **ein Worker pro logischem Kern** (dedizierte Threads mit `ThreadPriority.BelowNormal`, `IsBackground = true`), die Strategien per Round-Robin bzw. Bandit-Auswahl zuordnen. Die Inselstruktur (4 Inseln) kann erhalten bleiben: Worker ≈ Kerne, auf Inseln verteilt. Alternativ bewusst 1:1 portieren (Referenzverhalten) und danach optimieren. Das muss entschieden werden (siehe Offene Fragen).
- `TCriticalSection` → `lock` (Monitor, reentrant wie in Delphi) bzw. `System.Threading.Lock` (.NET 9+). Besser: Kommunikation über **lock-freie Ergebnis-Slots** (`Interlocked.CompareExchange` auf eine unveränderliche `Solution`-Referenz mit Kosten). Die Worker lesen dann per `Volatile.Read` den aktuellen Inselbesten statt per Push-Re-Init.
- Re-Init-Flags → `volatile bool` bzw. `Interlocked.Exchange` auf ein Request-Objekt (`ReinitRequest { Kind, PlanData, Text }`). Dann entfällt die Race bei `ReInitAll` und bei `SetMaxOption`.
- Tiefe `TPlan`-Kopien → kompakte Lösungsrepräsentation (z.B. `int[]` Termin-Index pro Spiel) und nur diese kopieren. `AssignOptimizedDates` ist genau das: Termine übernehmen. So werden GC-Last und Lock-Haltezeit drastisch reduziert.
- Polling (`Sleep(100)`) → `PeriodicTimer` (200 ms) oder ereignisgesteuert (Worker signalisiert eine Verbesserung per `Channel<Improvement>`).
- `Random` → **pro Worker eine eigene** `Random`-Instanz (bzw. Xoshiro), mit Seeds aus einem Master-Seed ableiten. Das macht Läufe reproduzierbar (optional, für Tests sehr wertvoll).
- `Durchlaufe` → `long` mit `Interlocked.Add` bzw. pro Worker `Volatile.Read`.
- Pause → `ManualResetEventSlim`/`SemaphoreSlim` (statt 100-ms-Sleep-Polling), Stop → `CancellationToken`.
- `WaitForThreadInit`/`KillThreads`-Busy-Waits → `Task.WhenAll`/`Barrier`/`CountdownEvent`.
- Aufwand: **M**. Wenig Code, aber die Korrektheit der Nebenläufigkeit und das Performance-Tuning sind anspruchsvoll. Zusammen mit PlanOptimizer und TPlanCalcThread als Einheit planen.

### Bugs/Auffälligkeiten (verifiziert)
- **Z. 197–204, `DynamicThreads[0]` statt `[i]`: bestätigt.** Im Zählschleifen-Rumpf steht `Thread := DynamicThreads[0];`. Dadurch ist `NumThreads` entweder 0 oder `DynamicThreads.Count` (= 10):
  - Hat `DynamicThreads[0]` bereits `ThreadBestType`, gilt `NumThreads = 10`, **keine** Umstellung.
  - Sonst `NumThreads = 0 < 3`, also wird `DynamicThreads[0]` umgestellt.
  - Folge: Die beabsichtigte **30-%-Obergrenze** („Nur, wenn nicht schon 30% der Threads mit dem Typ laufen“) **wirkt nicht**. Im Extremfall können alle 10 dynamischen Worker denselben Typ bekommen. Umgekehrt wird eine eigentlich erlaubte Umstellung blockiert, wenn zufällig das LRU-Element schon den Typ hat. Für die Migration **bewusst entscheiden**: Absicht (`[i]`) umsetzen oder das Verhalten des Originals beibehalten. Empfehlung: `[i]` (Absicht laut Kommentar). Danach die Performance vergleichen.
- Z. 195 bzw. 206: Der Kommentar sagt „30 %“, der Code prüft `< Count div 3` (= 3 von 10, also < 30 %).
- Z. 309–310: Der Kommentar „10 Dynamische Thread“ ist korrekt, weil `0..NUM_DYN_THREADS(9)` = 10. Die Konstante heißt aber irreführend „NUM“ und ist eigentlich der Maximalindex.
- Z. 409–413: `ReInitAll` setzt Flag und `ReinitString` **ohne Lock** (Race auf den referenzgezählten String mit Execute bzw. `DoReInitPlan`).
- Z. 427–436 zusammen mit `PlanOptimizer` Z. 187–188: `SetMaxOption` setzt `FlagReInitOptions := True` auch dann, wenn `ReInitPlanData = nil` ist. Hat die Insel zwischen `ReInit()` und `SetMaxOption()` des Optimierers (zwei getrennte Lock-Abschnitte) den Re-Init bereits verarbeitet, führt der nächste Poll `Plan.AssignPlanData(nil)` aus → **Access Violation**, und die gewünschte Maximalgewichtung geht verloren. Seltenes Timing, aber möglich.
- Z. 105: `Plan.CalculateKosten(-1)` außerhalb des Locks. Gleichzeitig kann der Optimierer über `GetActResult(Plan)` → `AssignOptimizedDates(Self.Plan)` lesend zugreifen. Solange `CalculateKosten` interne Caches von `Plan` schreibt, ist das eine Datenrace.
- Z. 157–174: Die Konsistenzprüfungen der Kosten sind wirkungslos (nur Trace, Korrektur auskommentiert). Die gemeldeten Kosten des Workers werden ungeprüft übernommen.
- `Durchlaufe` ist ein nicht atomarer Int64 und wird vom Optimierer-Thread geschrieben (`SpecialThread.Durchlaufe := 0`) sowie hier überschrieben (siehe PlanOptimizer.md).

## Offene Fragen
- Soll die C#-Version die feste Topologie 4 × (14 + 10) übernehmen (Vergleichbarkeit mit dem Original) oder an die Kernzahl anpassen? Empfehlung: konfigurierbar, Standard = Kernzahl.
- Soll der `[0]`-statt-`[i]`-Bug korrigiert werden (ändert das Suchverhalten)?
- Ist reproduzierbares Verhalten (fester Seed) gewünscht, z.B. für Regressionstests?
