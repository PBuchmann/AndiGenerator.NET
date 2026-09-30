# DialogProfile (DialogProfile.pas + DialogProfile.dfm)

**Kategorie:** Optimierung/Threading (Entwickler-Diagnose) + UI-Dialog
**Umfang:** 413 Zeilen .pas / 71 Zeilen .dfm
**Abhängigkeiten (uses):** PlanTypes (`TPlan`, `TMannschaftsKostenType`, `MyDouble`), PlanUtils (`StartWaitCursor`, `MyFormatInt`, `MyFormatFloat`, `FixControls`), SyncObjs (`TCriticalSection`), System.TypInfo (`GetEnumName`), System.Generics.Collections/Defaults; WinAPI `GetTickCount`; VCL: TMemo, TButton
**Verwendet von:**
- AndiGeneratorMain – `ButtonProfileClick` → `ShowDialogProfile(Kopie von GetCurrentViewPlan)`; der Button ist nur sichtbar mit `{$ifdef PROFILE}` (AndiGeneratorMain Z. 158).
- PlanMainThread – `AddThreadTypeToStatistics` (Z. 153) und `AddThreadInfoToStatistics` (Z. 218–230), jeweils nur unter `{$ifdef PROFILE}`.

## Zweck
Die Unit hat **zwei getrennte Aufgaben**, beide nur für den Entwickler:
1. **Mikro-Benchmark der Kostenfunktion** (nicht der Thread-Strategien!): misst, wie lange die einzelnen Teilkosten-Berechnungen und `FillTermine` für den aktuellen Plan dauern (je 10 001 Aufrufe, sequentiell im UI-Thread) und zeigt sie absteigend nach Zeit mit Prozentanteil.
2. **Trefferstatistik der Thread-Strategien**: ein thread-sicherer globaler Zähler, welcher Thread-Typ (Strategie aus `cThreadTauschPercent`, z. B. `'R'`, `'10'`, `'S2,50'`, `'M3,30'`) **eine Verbesserung** geliefert hat, plus die aktuelle Belegung der dynamischen Threads je Insel (`TPlanMainThread`). Wird vom Optimierer befüllt und im Dialog angezeigt.

**Wichtig:** Im ausgelieferten Build ist Profiling **deaktiviert**: Das .dproj definiert `PROFILE_1;SPECIALTHREAD` (nicht `PROFILE`), daher sind Profile-Button und Statistik-Aufrufe nicht einkompiliert. Die Unit wird trotzdem gelinkt (uses in AndiGeneratorMain und PlanMainThread).

## Inhalt / Struktur
### Typen
- `TMannschaftsKostenEvent = procedure(Sender; MannschaftsType: TMannschaftsKostenType) of object`
- `TTestAction` – ein Messpunkt: `Name`, `Millisecs: DWORD`, `MannschaftsType`, `EventCustom: TNotifyEvent` **oder** `EventMannschaft` (zwei überladene Konstruktoren).
- `TFormProfile` – Formular mit `Plan` (Kopie aus Hauptfenster), `PlanTemp` (weitere Kopie, eigen), `Actions: TObjectList<TTestAction>` (owns).
- `TTestActionComparer` – absteigend nach `Millisecs`; `TStatsComparer` – absteigend nach Trefferzahl.

### Benchmark (Teil 1)
- `ShowDialogProfile(Plan)` → Formular erzeugen, `Plan` setzen, `PlanTemp := TPlan.Create; Assign(Plan); TransferDateToRefDate()` (aktuelle Termine als Referenz sichern), `ShowModal`, `Free` (gibt `PlanTemp` in `FormDestroy` frei).
- `CreateActions` (in `FormCreate`) legt folgende Messpunkte an:
  | Messpunkt | Aufruf | Objekt |
  |---|---|---|
  | je `TMannschaftsKostenType` (16 Stück, Name via RTTI: `mktHalleBelegt`, `mktSisterGames`, `mktForceSameHomeGames`, `mktSperrTermine`, `mktAusweichTermine`, `mkt60Kilometer`, `mktEngeTermine`, `mkt2SpieleProWoche`, `mktSpielverteilung`, `mktKoppelTermine`, `mktAuswaertsKoppelTermine`, `mktZahlHeimSpielTermine`, `mktWechselHeimAuswaerts`, `mktAbstandHeimAuswaerts`, `mktRanking`, `mktMandatoryGames`) | `Plan.CalculateMannschaftsKosten(Typ)` | `Plan` |
  | „Spieltagkosten" | `Plan.CalculateSpielTagKosten(4 × out)` | `Plan` |
  | „Fehltermine" | `Plan.CalculateFehlterminKosten` | `Plan` |
  | „Nicht erlaubte Spieltage" | `Plan.CalculateFreeGameDateKosten` | `Plan` |
  | „Vereinsinterne Spiele am Anfang" | `Plan.CalculateSisterTeamsAmAnfang` | `Plan` |
  | „Filltermine 10%" | `PlanTemp.ProfileFillTermine(10)` | `PlanTemp` |
  | „Filltermine 2%" | `PlanTemp.ProfileFillTermine(2)` | `PlanTemp` |
  `TPlan.ProfileFillTermine(p)` (PlanTypes Z. 3691) = `TransferRefDateToDate(); NeuWuerfeln(ttNormal, 0, p); FillTermine();` → misst also den Kern-Schritt eines Worker-Threads (Referenzplan wiederherstellen, p % der Spiele neu würfeln, Termine neu füllen) – **ohne** `CalculateKosten`.
- `FireAction(Action)` → `GetTickCount` vor/nach einer Schleife `for i := 0 to 10000` (= **10 001 Aufrufe**) → `Millisecs`.
- `ShowResults(OnlyStats)` → WaitCursor; Memo := Thread-Statistik (Teil 2); wenn nicht `OnlyStats`: alle Actions nacheinander messen, Summe `Gesamt`, absteigend sortieren, Ausgabe `"<Name>: <ms>ms   <Anteil>%"`.
- Buttons: „Starten" → `ShowResults(False)`; `FormShow` → `ShowResults(True)` (nur Statistik); „Clear Stats" (`Button1`) → `ClearStats` + Dialog schließen; „Schließen" → `mrOK`.

### Thread-Statistik (Teil 2, unit-global)
- Globale Variablen: `StatisticsCriticalSection: TCriticalSection`, `ThreadStatistics: TDictionary<String, Integer>` (ThreadType → Anzahl Verbesserungen), `ThreadInfo: TDictionary<String, String>` (Inselname → kommagetrennte Liste der aktuellen dynamischen Thread-Typen). Erzeugt in `initialization`, freigegeben in `finalization`.
- `AddThreadTypeToStatistics(ThreadType)` – Zähler +1 (unter Lock). Aufruf in `TPlanMainThread.Execute`, wenn ein Worker-Ergebnis besser als das Inselergebnis ist (also pro übernommener Verbesserung, bis zu mehrmals pro 100-ms-Messzyklus).
- `AddThreadInfoToStatistics(ThreadName, Value)` – setzt/überschreibt Info (unter Lock). Aufruf nach jeder Verbesserung mit der aktuellen Belegung der `DynamicThreads`.
- `GetThreadStatistics(Strs)` – unter Lock: Überschrift „Treffer der Threadtypen", Liste absteigend nach Treffern, Leerzeile, „Dynamische Threads", dann alle ThreadInfo-Einträge (Dictionary-Reihenfolge = undefiniert).
- `ClearStats()` – leert **nur** `ThreadStatistics`, nicht `ThreadInfo`.

- UI (.dfm): Caption „Profile...", `Memo` (alClient, Ausgabe), unten „Clear Stats", „Starten", „Schließen".

## Fachliche Logik / Regeln
Keine Fachregeln – Messwerkzeug. Zusammenhang zur Optimierung:
- Die gemessenen Teilkosten sind genau die Bausteine von `TPlan.CalculateKosten(BreakByValue)` (PlanTypes ab ca. Z. 4100). Dort werden die Mannschaftskosten in zwei Gruppen abgearbeitet – `CalcSequenceFastPerformance` (11 günstige: EngeTermine, 2SpieleProWoche, 60Kilometer, AuswaertsKoppelTermine, MandatoryGames, KoppelTermine, ZahlHeimSpielTermine, WechselHeimAuswaerts, AbstandHeimAuswaerts, Ranking, SperrTermine) **vor** den Spieltagkosten und `CalcSequenceLowPerformance` (5 teure: AusweichTermine, HalleBelegt, SisterGames, ForceSameHomeGames, Spielverteilung) **danach**, mit Early-Exit bei Überschreiten von `BreakByValue`. Diese Reihenfolge ist sehr wahrscheinlich das Ergebnis dieses Benchmarks (Offene Frage) – günstige Teilkosten zuerst maximieren die Early-Exit-Ersparnis.
- Die Thread-Trefferstatistik ist das Werkzeug, mit dem die Strategie-Tabelle `cThreadTauschPercent` und die **dynamische Umverteilung** der 10 dynamischen Threads (in `TPlanMainThread`: erfolgreiche Strategie übernimmt den ältesten dynamischen Thread, sofern sie noch < 1/3 der dynamischen Threads belegt) beobachtet/abgestimmt wurde.

## Daten & Persistenz
Keine. Statistik nur im Speicher (Prozesslebensdauer).

## Threading / Performance
- **Benchmark läuft synchron im UI-Thread** (blockiert Oberfläche; WaitCursor). Der Optimierer ist während des Dialogs pausiert (`Optimizer.Paused := True` im Aufrufer) → Messung nicht durch Worker verfälscht, aber Turbo/Caches beeinflussen.
- Die Kostenfunktionen werden auf `Plan` wiederholt auf **unverändertem** Plan aufgerufen → misst „warme" Caches, keine realistische Mischung. `FillTermine`-Messungen ändern `PlanTemp` bei jedem Aufruf (setzen aber zuerst auf Referenz zurück → reproduzierbar, abgesehen von `Random`).
- Zeitauflösung `GetTickCount` ≈ 10–16 ms; bei 10 001 Iterationen ausreichend grob.
- Statistik: grobe Sperre (`TCriticalSection`) um Dictionary-Zugriffe; Aufrufe nur nach Verbesserungen → geringe Contention, kein Hot-Path.
- Die Strategie-Statistik misst **Anzahl Verbesserungen**, nicht Verbesserung pro CPU-Zeit oder Betrag der Verbesserung.

## Plattformabhängigkeiten
`GetTickCount` (WinAPI, DWORD, Überlauf nach 49,7 Tagen), VCL-Formular/Memo, `StartWaitCursor` (Screen.Cursor), RTTI `GetEnumName`.

## Migrationshinweise für C#
- Ziel:
  - Statistik → `AndiGenerator.Engine.Diagnostics.StrategyStatistics` (thread-safe: `ConcurrentDictionary<string,int>` mit `AddOrUpdate` oder `Interlocked` auf vorab angelegten Zählern; alternativ `System.Diagnostics.Metrics` Counter mit Tag „strategy"). Aktivierung per Konfigurations-Flag/`[Conditional("PROFILE")]` statt Compiler-Define.
  - Benchmark → **BenchmarkDotNet**-Projekt `AndiGenerator.Benchmarks` (reproduzierbar, statistisch sauber, mit Beispiel-XML-Datensätzen) statt In-App-Dialog; optional ein einfacher Diagnose-Dialog mit `Stopwatch` (hochauflösend) für Endanwender-Support.
  - Enum-Namen via `Enum.GetName`/`nameof`.
- Performance-Hinweise für die Migration (aus dieser Unit abgeleitet):
  - Die hier gemessenen Methoden sind die **Hot-Paths** des gesamten Programms (`CalculateMannschaftsKosten` je Typ, `CalculateSpielTagKosten`, `FillTermine`, `NeuWuerfeln`). In C# allokationsfrei implementieren (`struct`s, Arrays statt `List<T>`, `Span<T>`, keine LINQ im Hot-Path), damit GC die parallelen Worker nicht bremst.
  - Die Reihenfolge Fast/Low-Sequence mit Early-Exit **1:1 übernehmen** und nach der Portierung per BenchmarkDotNet neu verifizieren (relative Kosten können sich in .NET verschieben).
  - Strategie-Trefferstatistik ist nützlich, um nach der Portierung die Wirksamkeit der Thread-Strategien zu vergleichen (Delphi vs. C#).
- Aufwand: **S** (Statistik) / **M** (Benchmark-Projekt mit Testdaten).
- **Bugs/Auffälligkeiten:**
  - Z. 182: Division durch `Gesamt` ohne Nullprüfung → bei sehr kleinem Plan (alle Messungen 0 ms) Gleitkomma-Division durch 0 (Exception oder INF/NAN je nach FPU-Maske).
  - Z. 133: `Right.Millisecs - Left.Millisecs` auf `DWORD` → Integer-Überlauf bei großen Differenzen theoretisch möglich (praktisch irrelevant).
  - `ClearStats` leert `ThreadInfo` nicht; „Clear Stats" schließt zusätzlich den Dialog (unerwartet).
  - Z. 110: Schleife `0 to 10000` = 10 001 Iterationen (Off-by-one, nur kosmetisch).
  - In PlanMainThread (nicht diese Unit, aber für die Statistik relevant): Die Zählschleife „wie viele dynamische Threads haben bereits diesen Typ" liest immer `DynamicThreads[0]` statt `DynamicThreads[i]` → die 1/3-Grenze wird falsch berechnet (entweder 0 oder `Count` Treffer). Bei der Migration bewusst entscheiden.

## Offene Fragen
- Soll ein Profiling-/Diagnose-Modus in der C#-Version für Endanwender sichtbar sein oder nur als separates Benchmark-Projekt existieren?
- Wurde die Reihenfolge `CalcSequenceFastPerformance`/`CalcSequenceLowPerformance` mit diesem Dialog ermittelt, und gibt es dokumentierte Messergebnisse bzw. Referenz-Datensätze?
- Soll der Bug in der dynamischen Thread-Umverteilung (PlanMainThread, `DynamicThreads[0]`) korrigiert werden (verändert das Optimierungsverhalten)?
