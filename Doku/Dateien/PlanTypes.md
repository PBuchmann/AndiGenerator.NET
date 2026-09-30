# PlanTypes (PlanTypes.pas)

**Kategorie:** Kern-Datenmodell + Optimierung/Threading (Herzstück: Datenmodell, Kostenfunktion, Termin-Füllalgorithmus, Worker-Thread, Options-/Schedule-Persistenz)
**Umfang:** 9 534 Zeilen .pas / keine .dfm
**Abhängigkeiten (uses):**
- Projekt-eigene Units: `PlanUtils` (MyDouble = Double, MondayBefore, InternalWeekNumber, FixDateTime, MyFormatDate/-DateTime/-Float, MyStrToFloat, TraceString, TestDateRoutines), `PlanDataObjects` (TPlanData/TDataObject = XML-Datenmodell der Eingabedatei, Tag-/Attributkonstanten `nTeam`, `aDateTime` …, DateFromString, FormatTimeForExport, `TPlanData.FixLocation`)
- RTL/VCL: `Windows`, `Messages`, `Graphics` (werden im Code faktisch nicht benutzt, außer `FillMemory` aus Windows), `SysUtils`, `Classes` (TStrings/TStringList/TThread), `Contnrs`, `Math` (Power, Min, Max, CompareValue), `SyncObjs` (TCriticalSection), `System.Generics.Collections/Defaults` (TList<T>, TObjectList<T>, TDictionary, TObjectDictionary, TComparer), `TypInfo` (GetEnumName/GetEnumValue für XML-Optionen), `Xml.XMLDoc/XMLIntf` (IXMLDocument für Optionsdatei), `DateUtils`, `Variants` (VarToStr)
**Verwendet von:** praktisch alle anderen Units: `AndiGeneratorMain`, `PlanMainThread`, `PlanOptimizer`, `PlanPanel`, `EditMandatoryDatesDialog`, alle `Dialog*`-Units (DialogCreateTestData, DialogEditOne*, DialogEditSisterTeam, DialogEditTeamName, DialogFirstStart, DialogForMultiplePanel, DialogForSingleGewichtungsOptions, DialogForSinglePanel, DialogGetUpdates, DialogOptions, DialogPanel, DialogPanel60km(+Detail), DialogPanelAuswaertsKoppel(+Detail), DialogPanelFreeDays, DialogPanelHomeCoupleCompact(+Detail), DialogPanelHomeDays(+Detail), DialogPanelHomeRight(+Detail), DialogPanelLocationsCompact(+Detail), DialogPanelMainData, DialogPanelMandatoryDays, DialogPanelMultiTeams, DialogPanelOptionArray, DialogPanelPredefinedGames, DialogPanelRanking, DialogPanelSisterTeams(+Detail), DialogPanelTeams, DialogPrintSelect, DialogProfile, DialogQuestionHardErrors, DialogQuestionUseUserOptions, DialogSavePlan, DialogTeamPanel). `TPlanCalcThread` wird ausschließlich von `PlanMainThread` erzeugt/gesteuert (`Create`, `StartWithLists`, `SetThreadType`, `DoReInitPlan(Data)`, `GetActResult`, `Paused`).

---

## Zweck

`PlanTypes` enthält das **Laufzeit-Datenmodell eines Spielplans** (`TPlan` mit `TMannschaft`, `TGame`, `TWunschTermin`, Sperr-/Ausweich-/Koppelterminen, Schwestermannschaftsspielen, Pflichtspielzeiträumen, Setzliste) sowie die **gesamte Optimierungslogik** eines einzelnen Suchlaufs: Zufalls-Zerstörung eines Teils der Lösung (`NeuWuerfeln`), zufallsgesteuertes Wiederauffüllen der Termine (`FillTermine`), Round-Robin-Raster (`GenerateRaster`) und die **Kostenfunktion** (`CalculateKosten`) mit 16 Mannschafts- und 5 Plan-Kostenarten plus harten Fehlern. Der Worker-Thread `TPlanCalcThread` führt die Ruin-&-Recreate-Schleife aus. Zusätzlich: Laden eines Plans aus `TPlanData` (XML-Eingabe), Speichern/Laden der Gewichtungs-Optionen (`TCalculateOptions`, eigene XML-Datei) und Export des Spielplans als CSV (click-TT-Importformat) bzw. XML.

---

## Inhalt / Struktur

### Globale Konstanten / Variablen (Interface)

| Name | Wert / Typ | Bedeutung |
|---|---|---|
| `cThreadTauschPercent` | `('R','100','15','5','2','1','S1,100','S1,25','S1,10','S2,10','M1,10','M1,25','M1,100','M2,10')` | Strategien der 14 festen Worker je Insel (Parsing siehe `SetThreadType`) |
| `cMaxThreads` | 4 | Anzahl `TPlanMainThread`-Inseln (in PlanOptimizer genutzt) |
| `NUM_DYN_THREADS` | 9 | PlanMainThread erzeugt `0..NUM_DYN_THREADS` = **10** dynamische Worker (Start-Typ `'2'`) |
| `MinGameDistance` (var) | `EncodeTime(3,30,0,0)` (initialization) | Mindestabstand zweier Spiele am selben Tag (Koppeltermin), 3:30 h |
| `cMaxKostenNoHardError` (implementation) | `1e9` | Obergrenze für weiche Plan-Kosten; harte Fehler = `Anzahl × 10 × 1e9` |
| `cCalculateOptionsValues` | `(0, 1, 10, 100, 1000, 10000, 100000)` | Gewichtsfaktoren für `coIgnore … coExtremHoch` |
| `cCalculateOptionsDisplayInteger` | `(-1000, -2, -1, 0, 1, 2, 3)` | Anzeigewerte für UI-Summen |
| `cMannschaftsKostenTypeNamen`, `cPlanKostenTypeNamen`, `cCalculateOptionsNamen`, `cAuswaertsKoppelTypeName`, `cWunschterminOptionName` | Strings | Deutsche Anzeigenamen (UI) |
| `CacheHit/CacheMiss` | nur `{$ifdef CACHE_HIT_TEST}` | Debug-Zähler, nicht threadsicher |

### Enums / Sets

- `TWunschterminOption` (Set `TWunschterminOptions`): `woKoppelPossible/Soft/Hard` (zwei Heimspiele an einem Tag möglich/gewünscht/hohe Prio), `woKoppelDoubleDatePossible/Soft/Hard` (Doppelspieltag an zwei aufeinanderfolgenden Tagen), `woKoppelSecondTime` (künstlich erzeugter 2. Termin eines Heim-Koppeltags), `woAuswaertsKoppelSecondTime` (künstlich erzeugter Zusatztermin für Auswärtskoppel), `woAuswaertsKoppelHasSecondTime` (Wunschtermin besitzt Alternativzeit), `woKoppelNothing`.
- `TMannschaftsKostenType` (16): `mktHalleBelegt, mktSisterGames, mktForceSameHomeGames, mktSperrTermine, mktAusweichTermine, mkt60Kilometer, mktEngeTermine, mkt2SpieleProWoche, mktSpielverteilung, mktKoppelTermine, mktAuswaertsKoppelTermine, mktZahlHeimSpielTermine, mktWechselHeimAuswaerts, mktAbstandHeimAuswaerts, mktRanking, mktMandatoryGames`. **Die Enum-Namen sind persistiert** (Attributnamen in der Options-XML).
- `TPlanKostenType` (5): `pktGameDayOverlap, pktGameDayLength, pktLastGameDayOverlap, pktLastGameDayLength, pktSisterGamesAtBegin`.
- `TAuswaertsKoppelType`: `ktNone, ktSameDay, ktDiffDay, ktAllDay`.
- `THomeRightValue`: `hrNone, hrRound1, hrRound2`.
- `TCalculateOptionsValues`: `coIgnore, coSehrWenig, coWenig, coNormal, coHoch, coSehrHoch, coExtremHoch` (Namen persistiert).
- `TRoundPlaning`: `rpBoth, rpHalfRound, rpFirstOnly, rpSecondOnly, rpCorona` (Namen persistiert).
- `TTauschTyp`: `ttNormal, ttSpielTag, ttMannschaft, ttRaster`.
- `TMessageType`: `tmNoMessage, tmNormalMessage, tmDetailMessage` (steuert Cache-Nutzung und Textmeldungen der Kostenfunktionen).
- `TParallelGamesSuchRichtung`: `tpgBoth, tpgTop, tpgDown`.

### Records

- `TKostenValue` = `{Anzahl: Integer; AllCount: Integer; GesamtKosten: Double}` – Ergebnis einer Kostenart (Anzahl Verstöße, Soll-/Gesamtzahl für Anzeige, Kostenwert).
- `TMannschaftsKostenValue` = `array[TMannschaftsKostenType] of TKostenValue`.
- `TKostenCache` = `{Valid: boolean; Values: TMannschaftsKostenValue}`; `GesamtKosten = -1` ⇒ „nicht im Cache".
- `TMannschaftsKostenArray` = `array[TMannschaftsKostenType] of TCalculateOptionsValues`.
- Intern: `TKoppelDatesPerRound` (Zähler für Koppelkosten pro Runde).

### Klassen

| Klasse | Inhalt / Zweck |
|---|---|
| `TDateList` | `TList<TDateTime>` + `Assign`; für Sperr-/Ausweichtermine (sortiert, BinarySearch). |
| `TMannschaftsKosten` | `Values` (Kosten je Typ) + lazy `Messages: TStrings`; `ClearValues` (Anzahl 0, AllCount -1, Kosten 0), `getGesamtKosten`. |
| `TGame` | Ein Spiel Heim→Gast. Private Zustände: `FDate, FDateOptions, FDateMaxHome (=ParallelGames des Wunschtermins), FWunschTermin, FFixedDate, FNotNecessary` + **Referenzkopien** `FDateRef, FDateRefOptions, FDateRefMaxHome, FWunschTerminRef, FNotNecessaryRef`. Public: `MannschaftHeim/Gast`, `OverrideLocation`. Methoden: `setDate` (invalidiert KostenCache beider Mannschaften, pflegt `WunschTermin.assignedGame` bidirektional), `setEmptyDate`, `DateValid` (Date<>0), `isTheSame` (gleiches Datum + gleiche teamIds), `Location` (OverrideLocation vor WunschTermin.Location), `AsString`, `ClearAllValues`. |
| `TGameComparer`, `TGameList` | `TObjectList<TGame>`; `findSameGame` (linear), `SortByDate` (**Bubble-/Insertion-Sort** mit Rückschritt, da Liste meist vorsortiert; Tie-Break `compareStr(Heimname+' '+Gastname)`), `getFirstGameRefIndexByDate` (binäre Suche), `GetRefListe`. |
| `TScheduleGame`, `TSchedule`, `TScheduleComparer` | Namensbasierte, von Mannschaftsobjekten entkoppelte Spielliste (Date, Heim-/Gastname, Location) – Transportformat für `Assign`/Export. |
| `TWunschTermin` | Heimspieltermin einer Mannschaft: `Date` (mit Uhrzeit), `ParallelGames` (max. Heimspiele parallel in Halle), `Options`, `KoppelSecondTime`, `assignedGame` (Belegungs-Rückzeiger), `Location`. |
| `TWunschTerminList` | Sortiert nach Date; `Find(Date)` (lineare Suche, **exakter Double-Vergleich**), `Exists`, `ExistsIgnoreTime`. |
| `TAuswaertsKoppel(+List)` | Auswärtskoppelwunsch: `TeamA`, `TeamB` (Namen), `Option`, lazy gecachte `CachedMannschaftA/B` (Lookup per Name). |
| `THomeRight(+List)` | Heimrecht gegen ein bestimmtes Team in Vor-/Rückrunde; `getValueForTeam(teamName)`. |
| `TSisterGame`, `TSisterGames`, `TMapSisterGames` | Spiele von Vereins-Schwestermannschaften in **anderen** Ligen (Datum, Gender, Mannschaftsnummer, Heim/Gast, Location, `PreventParallelGame`, `ForceParallelHomeGames`). Map: `TObjectDictionary<Integer(Trunc(Date)), TSisterGames>`. |
| `TAuswaertsKoppelCachedTeams` | Cache: `TeamsOnDay` (Namen der Teams mit Auswärtskoppel am gleichen Tag, `ktSameDay/ktAllDay`), `TeamIdToTeamId` (teamId → Liste koppelbarer teamIds). |
| `TMannschaftDataCache` | Pro Mannschaft: `CachedAuswaertsKoppelTeams`, `CachedTeamsForForceHomeGames` (sortierte TStringList `gender#10teamName`). |
| `TMannschaft` | Team: `teamName, clubID, teamId, teamNumber, DefaultLocation, sisterGames, noWeekGame (Liste Teamnamen, bei denen 60-km-Regel gilt), SperrTermine, AusweichTermine, WunschTermine, AuswaertsKoppel, HomeRightCountRoundOne (-1 = keine Vorgabe), HomeRights, KostenAnzahl/Abstand/Wochentag (nur kopiert, nicht benutzt), SisterTeamsInPlan (gleiche clubID im selben Plan), gamesRef (alle Spiele dieses Teams, nach Datum sortiert, nicht besitzend), Index (Position in Plan.Mannschaften), DataCache, KostenCache, KostenCacheRef`. 16 `CalculateKosten*`-Methoden, Lade-Methoden, `IsTerminFree`, Koppel-Erkennung (`getKoppeledGame*`, `InternIsKoppeledGames`, `IsAuswaertsKoppeledGames`, `IsValidAuswaertsKoppelDate`), `GetHallenBelegung`, `GetParallelGames(Intern)`, `GetMissingParallelHomeGames`, `getTermineNotPossible` (Plausibilitätsprüfung für UI), `CreateAuswaertsKoppeledGamesDependenciesList`, `getRankingDiffs`, `getGameCount`. |
| `TMannschaftList` | `TObjectList<TMannschaft>` mit Namens-Comparer; `Assign` (Tiefkopie), `FindByName`/`FindByTeamId` (linear), `Sort` (nach Name, setzt `Index` neu). |
| `TPlanMandatoryTime(+List)` | Pflichtspielzeitraum `DateFrom..DateTo` mit min. `GameCount` Spielen; `AsString`, `IsTheSame`, `FindValueByDate`. |
| `TGewichtungMannschaften` | Pro-Team-Gewichtung: `TDictionary<teamId, TCalculateOptionsValues>` (Haupt) + `TDictionary<teamId, TMannschaftsKostenArray>` (Detail je Kostenart); Default `coNormal`. |
| `TCalculateOptions` | Alle Benutzeroptionen (siehe unten); `Load/Save` (XML), `Assign`, `Clear` (Defaults), `ToString`, `getMidDate1/2` (+automatisch), `get/setGewichtungPlan`, `getDiffToDefault` (Text für UI). |
| `TRoundInfo` | Runde `[DateFrom, DateTo)` (halboffen); `DateInRound`. |
| `TPlanNoGameDay(+List)` | Deklariert, aber in PlanTypes selbst nicht als Feld von TPlan verwendet (freie Tage stehen in `TPlan.FreeGameDays`). |
| `TPlanDataCache` | Plan-weite abgeleitete Flags + Soll-Spiele-Cache (siehe „Caches"). |
| `TPlan` | Gesamtplan (siehe unten). |
| `TPlanCalcThread` | Worker-Thread (siehe „Optimierungsalgorithmus" und „Thread-Sicherheit"). |
| `TSavedPlan` | Hülle für gespeicherte Pläne (FileName, Name, FileAge, Valid, Plan) – von UI genutzt. |

### TPlan – Felder

`dataCache`, `gender`, `Mannschaften`, `existingSchedule` (bereits existierender Spielplan aus Eingabe, relevant für Rückrunden-/Corona-Planung), `predefinedGames` (fest vorgegebene Spiele, `OverrideLocation`), `gamesAllowed` (alle zu planenden Spiele: n·(n−1), bei Doppelrunde ×2; besitzend), `gamesNotAllowed` (aus einem importierten Plan übernommene Spiele, die keinem erlaubten Spiel zuordenbar waren), `cachedGameList` (`"HeimId|GastId"` → Liste der Spiele dieser Paarung), `MandatoryGameDays`, `FreeGameDays` (`TDictionary<TDateTime,String>`, Key = Datum ohne Zeit; spielfreie Tage), `DateBeginRueckrundeInXML`, `DateBegin`, `DateEnd`, `PlanName`, `PlanId`, `LastOptimizedThread`, `RankingIndexe` (Setzliste: Rang → Mannschafts-Index), `FRounds`, `FOptions`, `AllDatesAreValid`.

### TPlan – wichtige Methoden (Kurzüberblick)

- **Laden:** `Load(PlanData)` → Stammdaten, `loadTeams` (+ `Mannschaften.Sort`), `RemoveNotValidSisterGames`, `AddSisterTeamsInPlan`, `GenerateGames`, `predefinedgames`, `LoadNoGameDays`, `LoadMandatoryGameDays`, `LoadRanking`, `existingschedule` (→ `AssignGames` + `assignExistingScheduleGames`), `ClearCache`. `LoadScheduleFromXml(FileName)`.
- **Kopieren:** `Assign` (= `AssignPlanData` + Schedule), `AssignPlanData` (Stammdaten + Optionen, behält eigene Termine per Name bei), `AssignOptimizedDates` (schnell: nur Termine per teamId-Paar übernehmen), `AssignOptions` (Optionen + `ModifyTermineFromOptions` + Termine wiederherstellen), `AssignSchedule(TSchedule)` (per Teamname), `AssignGames(TGameList)` (per teamId), `getSchedule`, `getCloneOfAllGames`.
- **Optimierung:** `NeuWuerfeln`, `NeuWuerfelnForGames`, `GenerateRaster`, `CreateRasterOneRound`, `ScheduleGameForRaster`, `FillTermine`, `FillTermineIntern`, `FillTermin`, `FillTerminIntern`, `FillAuswaertsKoppelTermin`, `FillPredefinedGames/Game`, `RemoveNotValidSecondTimeGames`, `TransferRefDateToDate`, `TransferDateToRefDate`, `ProfileFillTermine` (Profiling-Hilfe).
- **Gültigkeit:** `GameIsValid`, `TerminIsValid`, `IsInRoundToGenerate`, `IsRoundToGenerate`, `GetRoundNumberByDate`, `DateIsAllowedForWeekendGames`, `IsFreeGameDate`, `RemoveNotValidDates`, `MarkNotNecessaryGames`, `MarkSecondGameAsUnNecessary`.
- **Kosten:** `CalculateKosten(BreakByValue)`, `CalculateSpielTagKosten`, `CalculateFehlterminKosten`, `CalculateNotAllowedKosten`, `CalculateFreeGameDateKosten`, `CalculateSisterTeamsAmAnfang`, `CalculateMannschaftsKosten(Type)`, `GetMannschaftKostenFaktor`, `GetMannschaftKostenDisplayInteger`, `getHardErrorMessages`, `getNumSollGames`.
- **Export:** `SaveScheduleToCsv`, `SaveScheduleToXml`.
- **Info:** `Has60KilometerValues`, `HasKoppelTermine`, `HasAuswaertsKoppelTermine`, `HasAusweichTermine`, `HasMandatoryGameDays`, `HasRanking`, `HasForceSameHomeGames` (privat), `HasValuesForType`, `HasPossibleKoppelTermine`, `GetNumWunschDays`, `getMaxGamePerMannschaft`, `getSpieltagMondays`, `GetFirstDate/GetLastDate` (über alle Wunschtermine), `NoGames`.

### Freie Funktionen

`PercentRandom(p)` (`Random(100) < p`), `OptionIsKoppel`, `OptionIsKoppelHard/Soft`, `OptionIsKoppelSingleDate` (nur Heim-Koppel an einem Tag), `KoppelValueFromOptions`, `FormatDateForExport` (`dd.mm.yyyy`), `FormatBoolForExport`, `DayOfWeekAsString` (deutsch), `Multiply`, `Add` (Anzeige-Integer), `CalculateMaxAbweichung`, `CalculateStandardAbweichung` (Population-σ, bei ≤1 Wert: σ=0, Mittel=1), `AddIndented`, `TestCalcSequence` (Debug-Selbsttest in `initialization` bei `{$D+}`), `DoTests`. Kurios: eine lokale `function Date: TObject` (Z. 901) gibt `nil` zurück – ungenutzt, überdeckt aber `SysUtils.Date` innerhalb der Unit.

---

## Fachliche Logik / Regeln

### Runden (`CreateRoundInfo`, aufgerufen aus `GenerateGames`)

- **Normal** (`rpBoth/rpFirstOnly/rpSecondOnly/rpCorona`, keine Doppelrunde): Runde 0 = `[GetFirstDate−100, DateBeginRueckrundeInXML)`, Runde 1 = `[DateBeginRueckrundeInXML, GetLastDate+100)`.
- **Doppelrunde:** 4 Runden: `[First−100, Mid1)`, `[Mid1, RückrundeXML)`, `[RückrundeXML, Mid2)`, `[Mid2, Last+100)`. Mid1/Mid2 manuell (Optionen) oder automatisch: Montag in der Mitte der Spieltag-Montage (`SpieltageMondays[Count div 2]`, falls >2), sonst arithmetische Mitte.
- **Halbrunde** (`rpHalfRound`): eine Runde `[DateBegin, DateEnd)`; mit Doppelrunde zwei Runden `[DateBegin, Mid1)`, `[Mid1, DateEnd)` (Mid1 < DateBegin ⇒ Mid2).
- `IsInRoundToGenerate(Date)`: bei `rpBoth` immer true; sonst Rundennummer `r` (−1 ⇒ false). Ohne Doppelrunde: FirstOnly/HalfRound ⇒ `r = 0`; SecondOnly/Corona ⇒ `r ≥ 1`. Mit Doppelrunde: FirstOnly/HalfRound ⇒ `r ≤ 1`; SecondOnly/Corona ⇒ `r ≥ 2`.
- Bei Teilplanung (HalfRound, FirstOnly, SecondOnly, Corona) wird zu jedem terminierten Spiel das **Rückspiel als `NotNecessary`** markiert (`MarkSecondGameAsUnNecessary`: erstes nicht notwendiges, undatiertes Spiel Gast→Heim). Corona: für alle datierten Spiele (auch außerhalb der zu generierenden Runde).
- `ModifyTermineFromOptions`: bei `rpHalfRound` werden Sperr- und Wunschtermine außerhalb `[DateBegin, DateEnd]` gelöscht; künstliche Zusatztermine werden neu erzeugt (siehe Koppel).

### Spiele und Gültigkeit

- `GenerateGames`: für jedes geordnete Paar (i≠j) ein Spiel (Doppelrunde: zwei). Es gibt also Hin- und Rückspiel als getrennte Objekte.
- `TerminIsValid(WT, Game, IgnoreGame, AllSecondTimeGamesAreValid)`:
  1. Termin ist kein freier Spieltag (`FreeGameDays`),
  2. `IsTerminFree` für Heim- **und** Gastmannschaft,
  3. keine gleiche Paarung (gleiche Heim/Gast-Richtung) in derselben **Hauptrunde** (`round div 2`; verhindert zweimal dasselbe Heimspiel vor Weihnachten bei Doppelrunde bzw. doppeltes Spiel generell),
  4. kein Rückspiel (Gast/Heim vertauscht) in **derselben Runde**.
- `IsTerminFree` (je Mannschaft): kein anderes Spiel am selben Kalendertag, außer
  - der Plan hat überhaupt Koppel- oder Auswärtskoppeltermine, **und**
  - zeitlicher Abstand ≥ `MinGameDistance` (3:30 h), **und**
  - entweder gültiger Auswärtskoppel (self ist Gast in beiden Spielen und `IsValidAuswaertsKoppelDate`), oder self ist Heimmannschaft und beide Spiele liegen auf Koppel-Wunschterminen.
  - Zusatztermine (`woKoppelSecondTime` für Heimteam, `woAuswaertsKoppelSecondTime` für Gastteam) sind nur zulässig, wenn im Tagesvergleich ein gültiger Koppel gefunden wird (Result wird im Schleifenrumpf explizit wieder auf True gesetzt) oder `AllSecondTimeGamesAreValid`.
- `GameIsValid(Game)`: außerhalb der zu generierenden Runde ungültig (Ausnahme SecondOnly/Corona: Spiel ist in `existingSchedule` oder `predefinedGames`); sonst muss ein Wunschtermin des Heimteams mit **exakt gleichem Datum+Uhrzeit** existieren und `TerminIsValid(WT, Game, Game)` gelten; ohne Wunschtermin nur gültig, wenn vordefiniertes Spiel.
- `IsValidAuswaertsKoppelDate(D1, D2, teamA, teamB)`: |Tage| ≤ 1 und ein Wunsch (A,B) oder (B,A) mit Option `ktSameDay` (gleicher Tag, Abstand ≥ 3:30 h), `ktDiffDay` (genau 1 Tag Differenz) oder `ktAllDay` (≤1 Tag, bei gleichem Tag ≥3:30 h).
- **Koppel-Erkennung** in der sortierten `gamesRef`: `getKoppeledGameAfter(i)` = Spiel i+1, wenn `InternIsKoppeledGames` (Heim-Koppel: beide Heimspiele des Teams, ≤1 Tag auseinander, beide mit Koppel-Option; Auswärtskoppel: beide Auswärtsspiele des Teams und gültiges Auswärtskoppel-Datum) **und** i+1 nicht selbst wieder an i+2 gekoppelt ist (keine 3er-Koppel; rekursiv). `getKoppeledGameBefore/getKoppeledGame` bauen darauf auf.
- **60-km-Regel:** Mannschaft X hat Liste `noWeekGame` (Teamnamen). Auswärtsspiele von X bei einem Team aus dieser Liste müssen am Wochenende liegen (Sa/So; Fr nur wenn `FreitagIsAllowedBy60km`).
- **Hallenbelegung:** Zeitfenster ±1:29 h, gleiche Location; zählt Heimspiele von Schwestermannschaften anderer Ligen (`sisterGames`) und Heimspiele von Vereinsmannschaften im gleichen Plan (nicht gegen self). Verstoß, wenn Belegung ≥ `ParallelGames` des Wunschtermins (nur wenn `ParallelGames > 0`).
- **Parallele Spiele (Schwestermannschaften):** Zeitfenster ±3:59 h. Bei `tpgBoth` zählen Schwesterspiele anderer Ligen mit `PreventParallelGame`; danach rekursiv Nachbarmannschaften derselben Altersklasse (`gender`) mit Nummer ±1 (II→III usw., immer aktiv), sowie Teams gleicher clubID im selben Plan mit `teamNumber` ±1.
- **Synchrone Heimspiele** (`ForceParallelHomeGames`): an jedem Heimspieltag sollen die markierten Schwestermannschaften ebenfalls ein Heimspiel am selben Kalendertag haben.
- `getTermineNotPossible`: UI-Plausibilitätsmeldungen (alle Termine eines Gegners gesperrt; 60-km-Regel bei einem Gegner unerfüllbar; nur Koppeltermine bei gerader Teamzahl; Auswärtskoppelwunsch ohne passende Wunschtermine; unbekanntes Team im Auswärtskoppel).
- **Setzliste/Ranking:** `LoadRanking` baut `RankingIndexe` (Rang → Mannschaftsindex; fehlende Teams hinten angehängt), nur wenn `<ranking active="true">`.

### Heimkoppel-Zusatztermine (`ModifyTermineFromOptions`)

Für jeden Wunschtermin mit `woKoppelPossible/Soft/Hard` wird eine Kopie mit `Date := KoppelSecondTime` und Option `+woKoppelSecondTime` angelegt; für jeden mit `woAuswaertsKoppelHasSecondTime` eine Kopie mit `+woAuswaertsKoppelSecondTime`. Vorher werden alte Zusatztermine entfernt. `KoppelSecondTime` = Tagesdatum + `couplesecondtime` (beim Laden, `FixDateTime` normalisiert Rundungsfehler).

---

## Optimierungsalgorithmus im Detail

### Worker-Schleife `TPlanCalcThread.Execute`

```
OptKosten := Plan.CalculateKosten(-1); Plan.TransferDateToRefDate()
loop until Terminated:
  if Paused: Sleep(100); continue
  if LowPrioThread and DurchLaufAfterReInit > 10000: Sleep(50)
  Plan.TransferRefDateToDate()          // letzte akzeptierte Lösung wiederherstellen (+Kosten-Cache)
  Plan.FillPredefinedGames()            // fixe Spiele erzwingen
  Plan.NeuWuerfeln(TauschTyp, TauschAnzahl, TauschPercent)   // Ruin
  Plan.FillTermine()                    // Recreate
  Kosten := Plan.CalculateKosten(OptKosten)                  // Early-Exit: -1 wenn > OptKosten
  if Kosten >= 0 and (Kosten < OptKosten or OptKosten < 0):  // nur echte Verbesserung (strikt <)
     lock Parent: Kosten := Plan.CalculateKosten(-1)   // voll, füllt alle Cache-Werte
                  Plan.TransferDateToRefDate(); OptKosten := RetKosten := Kosten
                  Plan.LastOptimizedThread := Name; OptPlan.AssignOptimizedDates(Plan)
  if ReInitPlanFlag or ReInitPlanDataFlag:
     lock Parent: ReInitPlan → Plan.AssignOptimizedDates(ReInitPlan), Kosten neu, Ref übernehmen, OptPlan := Plan
                  ReInitPlanData → Plan/OptPlan.AssignPlanData(...) + RemoveNotValidDates + TransferDateToRefDate,
                                   OptKosten := OptPlan.CalculateKosten(-1)
  Sleep(0)
```

- Akzeptanz ist **rein greedy** (nur strikt bessere Lösungen); keine Simulated-Annealing-Komponente. Diversität entsteht durch verschiedene Strategien und durch den Austausch über `PlanMainThread` (ReInit mit besserer Lösung anderer Threads).
- `SetThreadType(TauschArt)`: `'R'` ⇒ `ttRaster`, LowPrio; `'S<n>,<p>'` ⇒ `ttSpielTag`, `TauschAnzahl = n` (nur **eine Ziffer**: `TauschArt[2]`), `TauschPercent = p` (ab Zeichen 4); `'M<n>,<p>'` ⇒ `ttMannschaft`; sonst Zahl ⇒ `ttNormal` mit Prozent, LowPrio wenn ≥ 30. `MyStrToFloat` nutzt '.' als Dezimaltrenner.
- Konstruktor: `Priority := tpLower`, eigene `Plan`- und `OptPlan`-Instanz. `StartWithLists` kopiert `Plan` (Deep-Copy via `Assign`) + `RemoveNotValidDates` in beide.

### `TransferRefDateToDate` / `TransferDateToRefDate`

- `TransferDateToRefDate`: für alle `gamesAllowed` `FDate/Options/MaxHome/WunschTermin/NotNecessary → *Ref`; pro Mannschaft `KostenCacheRef := KostenCache`. = „Commit" der aktuellen Lösung.
- `TransferRefDateToDate`: für alle nicht-fixen Spiele `setDate(*Ref…)` (das invalidiert jeweils die KostenCaches) und danach pro Mannschaft `KostenCache := KostenCacheRef`. = „Rollback" auf die letzte akzeptierte Lösung **inklusive** der zugehörigen Teilkosten, sodass im nächsten Durchlauf nur die Mannschaften neu berechnet werden, deren Spiele sich tatsächlich ändern.

### `FillPredefinedGames` / `FillPredefinedGame`

Für jedes vordefinierte Spiel: Spiel gleicher Paarung mit identischem Datum suchen, sonst **das letzte** undatierte Spiel dieser Paarung (bzw. `games[0]`); `setDate(..., fixedDate=True)` mit passendem Wunschtermin (exakter Datumsvergleich) oder ohne. Bei `rpSecondOnly/rpCorona` zusätzlich alle Spiele aus `existingSchedule`, die **nicht** in der zu generierenden Runde liegen (Vorrunde bleibt fix). Fixe Spiele werden von `NeuWuerfeln`/`TransferRefDateToDate`/`ClearGameList` nie verändert.

### `NeuWuerfeln(TauschTyp, TauschAnzahl, TauschPercent)` – Ruin

- `ttRaster`: `GenerateRaster()` (komplette Neuaufstellung, s. u.).
- `ttMannschaft`: `TauschAnzahl`-mal zufällige Mannschaft ziehen (mit Zurücklegen), alle deren `gamesRef` sammeln (Duplikate möglich) → `NeuWuerfelnForGames(liste, p, WithRueckGame=false)`.
- `ttSpielTag`: `TauschAnzahl`-mal zufälligen Index `j < Mannschaften[0].gamesRef.Count` ziehen, von jeder Mannschaft das `j`-te Spiel (gamesRef ist nach Datum sortiert, undatierte `Date=0` stehen vorne) → `NeuWuerfelnForGames(liste, p, true)`. „Spieltag" = j-tes Spiel je Team (Näherung).
- `ttNormal`: `NeuWuerfelnForGames(gamesAllowed, p, true)`.
- `NeuWuerfelnForGames(Games, Percent, WithRueckGame)`:
  - `Percent = 0` ⇒ nichts; `Percent = 100` ⇒ alle nicht-fixen Spiele der Liste leeren.
  - sonst `Ziel = max(1, min(n, Trunc(n·p/100)))` Spiele zufällig ziehen; bereits leere Spiele werden verworfen (zählen nicht). Pro gezogenem Spiel: falls `WithRueckGame` und Koppel im Plan: gekoppeltes Heimspiel (`MannschaftHeim.getKoppeledGame`) und alle Auswärtskoppel-Abhängigkeiten (`MannschaftGast.CreateAuswaertsKoppeledGamesDependenciesList`) ebenfalls leeren; Spiel leeren; bei `WithRueckGame` **alle** Spiele der Paarung in beiden Richtungen leeren (`ClearAllGamesOfMannschaften`).

### `GenerateRaster` (Round-Robin, Strategie 'R')

1. Alle nicht-fixen Spiele leeren; bei Teilplanung `MarkNotNecessaryGames`.
2. Mannschaftsliste kopieren und 101 zufällige Vertauschungen; Liste `SpieltagIndexe = 0..n−2` ebenso 101-mal zufällig vertauschen.
3. Für jede Runde `i`: `CreateRasterOneRound(Teams, SpieltagIndexe, Round.DateFrom, Round.DateTo, RueckRunde := (i mod 2 = 0))`:
   - `getSpieltagMondays` (Montage aller Wochen mit mindestens einem Wunschtermin irgendeines Teams in der Runde), zufällig reduziert auf `n−1` Wochen.
   - Kreisverfahren (Wikipedia „Round-robin tournament – Scheduling algorithm"): bei gerader Teamzahl ist das letzte Team „Filler", `MannschaftCount = n−1` (ungerade). Für Team `m` und Spieltag `s`: `m2 = (−SpieltagIndexe[s] − m) mod MannschaftCount`; bei `m2 = m` spielt `m` gegen Filler; nur `m2 ≥ m` wird verarbeitet.
   - Heimrecht: `RueckSpiel := RueckRunde`, invertiert bei geradem `s` und nochmals in der zweiten Hälfte der Spieltage (`s ≥ Count/2`) → alterniert H/A.
   - `ScheduleGameForRaster(Heim, Gast, Monday)`: erster (chronologischer) Wunschtermin des Heimteams in dieser Woche, der in der zu generierenden Runde liegt und `TerminIsValid` ist → erstes nicht-notwendiges, undatiertes Spiel der Paarung setzen (+ Rückspiel ggf. NotNecessary).
4. Nicht zugeordnete Spiele füllt anschließend `FillTermine` zufällig auf.

### `FillTermine` / `FillTermineIntern` – Recreate

```
FillTermine:
  FillTermineIntern(AllSecondTimeGamesAreValid = True)
  if HasAuswaertsKoppelTermine or HasKoppelTermine:
     RemoveNotValidSecondTimeGames()     // Spiele auf Zusatzterminen, die GameIsValid (strikt) nicht erfüllen → leeren
     FillTermineIntern(False)            // zweiter Durchgang mit strikten Regeln
```

`FillTermineIntern`: ggf. `MarkNotNecessaryGames`; Liste aller Spiele mit `Date = 0`; solange nicht leer: zufälliges Spiel ziehen; `NotNecessary` ⇒ entfernen; sonst `FillTermin(Game)`; falls Auswärtskoppel im Plan **mit 70 % Wahrscheinlichkeit**: Abhängigkeitsliste des Gastteams zum Heimteam holen, ein zufälliges Spiel daraus mit `FillAuswaertsKoppelTermin(spiel, Game.Date)` terminieren; Spiel aus Liste entfernen. Am Ende `SortMannschaftsDates` (gamesRef jeder Mannschaft sortieren).

- `FillTermin(Game)`: Indexliste 0..k−1 aller Wunschtermine des **Heimteams** → `FillTerminIntern`.
- `FillAuswaertsKoppelTermin(Game, Date)`: nur Wunschtermine des Heimteams mit |Tag − Date| ≤ 1 → `FillTerminIntern`.
- `FillTerminIntern(IndexList, Game)`: zufällige Ziehung ohne Zurücklegen: Kandidat verworfen wenn `WunschTermin.assignedGame` belegt, nicht in zu generierender Runde, oder Auswärtskoppel-Zusatztermin (ohne Heim-Koppel-Zusatz), bei dem das Heimteam nicht in `TeamsOnDay` des Gastes steht; sonst `TerminIsValid` ⇒ `setDate` + ggf. Rückspiel NotNecessary, Abbruch. Entfernen per `System.Move` (Open-Array-Kopie).
- Findet sich kein gültiger Termin, bleibt das Spiel undatiert ⇒ harter Fehler in der Kostenfunktion (1e10).

### `CalculateKosten(BreakByValue)` mit Early-Exit

Reihenfolge (nach jedem Block Prüfung `BreakByValue >= 0 and Result > BreakByValue` ⇒ `-1`):
1. `CalculateFehlterminKosten` (Check)
2. `+ CalculateFreeGameDateKosten` (Check)
3. `+ CalculateSisterTeamsAmAnfang` (wenn Gewicht ≠ Ignore; **kein** Check direkt danach)
4. `CalcSequenceFastPerformance` (vor jedem Typ ≠ Ignore Check): `mktEngeTermine, mkt2SpieleProWoche, mkt60Kilometer, mktAuswaertsKoppelTermine, mktMandatoryGames, mktKoppelTermine, mktZahlHeimSpielTermine, mktWechselHeimAuswaerts, mktAbstandHeimAuswaerts, mktRanking, mktSperrTermine` (Check)
5. Spieltagskosten (Breite+Overlap+LastBreite+LastOverlap), wenn `SpieltagGewichtung` **oder** `SpieltagOverlapp` ≠ Ignore
6. `CalcSequenceLowPerformance` (vor jedem Typ Check): `mktAusweichTermine, mktHalleBelegt, mktSisterGames, mktForceSameHomeGames, mktSpielverteilung` (Check)
7. `+ CalculateNotAllowedKosten` (kein Check mehr)

`CalculateMannschaftsKosten(Typ)` = Σ über alle Mannschaften `CalculateKostenForType(..., tmNoMessage)` (legt dafür **pro Mannschaft und Typ ein `TMannschaftsKosten`-Objekt an** – Allokation im Hot-Path). `TestCalcSequence` stellt sicher, dass jeder Typ genau einmal in einer der Sequenzen vorkommt. Vergleich im Thread: `Kosten < OptKosten` (strikt).

### `AssignOptimizedDates(Source)`

`clearAllDates` (alle Termine, Wunschtermin-Refs, gamesNotAllowed, Caches), dann für jedes `Source.gamesAllowed[i]` per `findGames(HeimId, GastId)` das erste undatierte Spiel gleicher Paarung setzen (Options/MaxHome/Fixed/NotNecessary übernommen, Wunschtermin per `Find(Date)` im eigenen Plan); fehlt die Paarung ⇒ Exception `'Plan.Assign: game wurde nicht gefunden'`. Danach ggf. `MarkNotNecessaryGames` und `SortMannschaftsDates`. Voraussetzung: identische Stammdaten (Teams/teamIds) in beiden Plänen.

---

## Kostenfunktion

### Gewichtung

- **Plan-Kosten:** Faktor `w = cCalculateOptionsValues[Option]` direkt (Normal = 100).
- **Mannschafts-Kosten:** `F = GetMannschaftKostenFaktor(M, T) = 100 · g(Main[T]) · g(Team[M].Main) · g(Team[M].Detail[T])` mit `g(v) = cCalculateOptionsValues[v]/100` bzw. 0 bei `coIgnore` (`Multiply`). Default (alles Normal) ⇒ **F = 100**; „sehr wenig" (1) ⇒ ×0,01; „extrem hoch" (100 000) ⇒ ×1000. Pro-Team-Werte stammen aus `TGewichtungMannschaften` (Key = teamId, Default Normal).
- Anzeige: `GetMannschaftKostenDisplayInteger` = Summe der `cCalculateOptionsDisplayInteger` (−2…3), sobald eine Stufe `coIgnore` ist ⇒ −1000.
- Ein Mannschafts-Kostentyp mit globalem Gewicht `coIgnore` wird in `CalculateKosten` gar nicht berechnet.

### Harte Fehler (unabhängig von Gewichten)

| Kosten | Berechnung |
|---|---|
| Fehltermine | `#(gamesAllowed mit Date = 0 und nicht NotNecessary) × 10 × 1e9` |
| Freie Spieltage | nur wenn `FreeGameDays` nicht leer: `#(Spiele auf freiem Tag und in zu generierender Runde) × 1e10` |
| Nicht erlaubte Spiele | nur wenn `not AllDatesAreValid` **oder** Koppel/Auswärtskoppel im Plan: `(#gamesNotAllowed + #datierte, notwendige gamesAllowed mit not GameIsValid) × 1e10`. Sonst 0 (Annahme: `FillTermin` wählt nur gültige Termine). |

`cMaxKostenNoHardError = 1e9` deckelt nur die **Plan-Kosten** (Breite, Overlap, LastBreite, LastOverlap, SisterTeamsAmAnfang), **nicht** die Mannschaftskosten.

### Plan-Kostentypen (`TPlanKostenType`)

| Typ | Berechnung |
|---|---|
| `pktGameDayLength` (Länge der Spieltage, `SpieltagGewichtung`) | Für jeden „Spieltag" i (= i-tes Spiel jedes Teams nach Datum, nur Spiele in zu generierender Runde): `L = (Trunc(max) − Trunc(min)) · n/10` (weniger Teams ⇒ breiter erlaubt); `Diff = max(0, L−6)/7`, falls `L>0` zusätzlich `+ min(0.99, σ(Tage)/(L/2))`. Kosten `+= Diff³ · w`. |
| `pktLastGameDayLength` (`LastSpieltagGewichtung`) | Für den letzten Spieltag zusätzlich `Diff³ · wLast · 5`. |
| `pktGameDayOverlap` (`SpieltagOverlapp`) | Für Spieltag i: Vergleich seines frühesten Datums mit den spätesten Daten aller vorherigen Spieltage (rückwärts, Faktor 5, 25, 125 …): wenn `Trunc(min_i) ≤ Trunc(max_j)` ⇒ `(1 + Tage/8) · Faktor`. Summe · w. |
| `pktLastGameDayOverlap` (`LastSpieltagOverlapp`) | Beim letzten Spieltag: `value · 20` (es bleibt der Wert des **zuletzt iterierten**, d. h. ältesten überlappenden Spieltags) · wLastOverlap. |
| `pktSisterGamesAtBegin` (Vereinsinterne Spiele am Anfang) | `getTeamsNotAtBegin`: je Team mit Vereinskameraden im Plan und je zu generierender Runde: Anzahl Spiele gegen Nicht-Vereinsteams, die vor Abschluss aller vereinsinternen Spiele liegen. Kosten `Fehler² · 300 · w`. |

Die Spieltagkosten werden nur berechnet, wenn `SpieltagGewichtung` oder `SpieltagOverlapp` ≠ Ignore (die „Last"-Gewichte allein aktivieren sie nicht).

### Mannschafts-Kostentypen (`TMannschaftsKostenType`), F = Gewichtsfaktor (Default 100)

| Typ (Anzeigename) | Was wird berechnet | Formel |
|---|---|---|
| `mktHalleBelegt` (Hallenbelegung) | Nur wenn Schwesterspiele oder Vereinsteams im Plan existieren. Pro Heimspiel mit `DateMaxHome>0`: Belegung B (±1:29 h, gleiche Location). Wenn `B ≥ MaxHome`: `Anzahl += B − MaxHome + 1`. | `(Anzahl·10)² · 500 · F` |
| `mktSisterGames` (parallele Spiele) | Pro Spiel (H+A): parallele Spiele N nach `GetParallelGames` (±3:59 h, s. o.). `v += N³`. | `v^1.5 · 20 · F` |
| `mktForceSameHomeGames` (synchrone Heimsp.) | Pro Heimspiel: Anzahl der Pflicht-Schwesterteams ohne Heimspiel am selben Kalendertag. | `Anzahl³ · 10 · F` |
| `mktSperrTermine` | Spiele (H oder A, in Runde) auf einem Sperrtag (Binärsuche). `v = V/|Sperr| · 20 · (NumWunschDays/50)` (mehr Spieltage ⇒ Sperrtermine wichtiger). | `v³ · 200 · F` |
| `mktAusweichTermine` | Heimspiele auf Ausweichterminen (nur wenn der Plan Ausweichtermine hat). | `N² · 10 · F` |
| `mkt60Kilometer` (60km Regel) | Auswärtsspiele bei Teams aus `noWeekGame`, nicht Sa/So (Fr optional). | `(N·10)² · F` |
| `mktEngeTermine` (3-Tage-Abstand) | Aufeinanderfolgende Spiele mit 0–3 Tagen Abstand, die nicht gekoppelt sind: `v += (4/(d+1))^4.3`. | `v² · F` |
| `mkt2SpieleProWoche` | Aufeinanderfolgende Spiele in derselben Woche (`InternalWeekNumber = (Trunc(D)−2) div 7`, Mo–So), nicht gekoppelt. | `N² · 400 · F` |
| `mktSpielverteilung` | Für jede zu generierende Runde und jede Lücke (Rundenbeginn→1. Spiel, Spiel→Spiel, letztes Spiel→Rundenende+1) die erwartete Zahl Spiele im Intervall `getNumSollGames = (n−1)/DateCount · DateInIntervall` (Wunschtermine aller Teams der Runde ohne freie Tage, Koppeltermine zählen 2); ÷1,5 bei angrenzendem Koppel, +0,5 an Rundenrändern. σ dieser Werte. `Anzahl = −1`. | `(σ·5)^5 / 10 · F / gamesRef.Count · 20` |
| `mktKoppelTermine` (Heimkoppel) | Pro Runde Soll = min(harte/2, `(n−1) div 4`) + 0,25·weiche/2 (gekappt); Ist = gekoppelte Spielpaare (hart +1, weich +0,25; „possible" zählen nicht). `v = Σ max(0, Soll−Ist)`. | `(v·10)² · F` |
| `mktAuswaertsKoppelTermine` | Jede im Wunsch genannte Mannschaft soll `Rounds.Count div 2` mal Ziel eines Auswärtskoppels sein; tatsächliche Auswärtskoppel-Paare zählen herunter; Anzahl = Σ Rest > 0. Dämpfung `Factor = 1 − 0.75·max(0,min(6,W−2))/6` (W = Anzahl Wunsch-Teams). | `(Anzahl·5)² · 4 · F · Factor` |
| `mktZahlHeimSpielTermine` (Ungleich H/A) | Heimspiele pro Runde vs. Ziel (`gamesRef.Count·0,5/Rounds`, Halbrunde+Doppelrunde 0,25; mit `HomeRightCountRoundOne ≥ 0`: gerade Runden = Wert, ungerade = `n−1−Wert`); `Fehler = Trunc(|Ist−Ziel|)`. Zusätzlich Heimrechte (`hrRound1` ⇒ Heimspiel in gerader Runde) ⇒ `TeamHomeRight`. Corona/Halbrunde ohne DR: eine Gesamtrunde, Ziel = notwendige Spiele/2. | `(Anzahl + 2·TeamHomeRight)² · 300 · F` |
| `mktWechselHeimAuswaerts` (Wechsel H/A) | Folgen gleicher H/A-Art (nicht gekoppelt): Streak `k`, `v += k⁴`. | `v²/30 · F` |
| `mktAbstandHeimAuswaerts` (Abstand H/A) | Pro Gegner: Index-Abstand (in gamesRef) zwischen Hin- und Rückspiel(en); `CalculateMaxAbweichung` (max. gewichtete Abweichung vom Mittel, unter Mittel ×2, über Mittel ×1) ÷ Mittelwert. Entfällt bei Halbrunde/FirstOnly/Corona ohne Doppelrunde. **Exception bei > 30 Mannschaften.** | `(v·6)^6 / 5000 · F` |
| `mktRanking` (Setzliste) | Nur wenn Setzliste aktiv. Pro Spiel (chronologisch, je Runde) Ziel `z` = n−1, n−2, …; `diff = |Rang_self − Rang_Gegner| − z`; wenn `diff>0` und `z>0`: `v += Faktor · diff / z^1.25` (Faktor 4 in letzter Runde). ⇒ Spiele zwischen Rang-Nachbarn gehören ans Rundenende. | `(v/8)³ · F` |
| `mktMandatoryGames` (Pflichtspieltage) | Pro Pflichtzeitraum in zu generierender Runde: fehlende Spiele `GameCount − Ist` (Datumsvergleich auf Tagesbasis). | `N² · 1000 · F` |

`AllCount`/`Anzahl` dienen der UI-Anzeige (z. B. „x von y Sperrterminen verletzt"). Mit `WithMessages ≠ tmNoMessage` werden Klartextmeldungen erzeugt (Cache wird dann nicht gelesen, aber geschrieben).

---

## Caches & Referenzdaten

| Cache | Ort | Inhalt | Invalidierung | Hinweise für Portierung |
|---|---|---|---|---|
| `KostenCache` | `TMannschaft` | Letzte `TKostenValue` je Kostentyp; `GesamtKosten = −1` ⇒ ungültig, `Valid` ⇒ mind. ein Wert gesetzt | `TGame.setDate` ruft `ClearKostenCache` für Heim- **und** Gastteam; `ClearCache` | Korrektheit hängt davon ab, dass **jede** Kostenart nur von Spielen der eigenen Mannschaft abhängt. Ausnahmen (`mktHalleBelegt`, `mktSisterGames`, `mktForceSameHomeGames` hängen bei Vereinsteams im Plan von deren Spielen ab) lesen den Cache nur, wenn `SisterTeamsInPlan.Count = 0`. |
| `KostenCacheRef` | `TMannschaft` | Snapshot des KostenCache zur akzeptierten Lösung | Gesetzt in `TransferDateToRefDate`, zurückgespielt in `TransferRefDateToDate` | Ermöglicht inkrementelle Kosten: nach dem Rollback sind alle Caches wieder gültig, nur durch `NeuWuerfeln`/`FillTermine` berührte Teams werden neu berechnet. **Größter Performance-Hebel**. |
| `*Ref`-Felder in `TGame` | `FDateRef, FDateRefOptions, FDateRefMaxHome, FWunschTerminRef, FNotNecessaryRef` | Zustand der akzeptierten Lösung | wie oben | In C# als paralleles Array „committed solution" + „working solution" (Array.Copy). |
| `TPlanDataCache` (`TPlan.dataCache`, lazy über `getCache`) | Plan | `Has60KilometerValues, NumWunschDays` (Anzahl verschiedener Tage mit Wunschtermin), `HasAuswaertsKoppelTermine, HasKoppelTermine, HasAusweichTermine, HasMandatoryGameDays, HasForceSameHomeGames`, `sollGamesByDate` (`TDictionary<"TagVon TagBis", Double>`) | `TPlan.ClearCache` (in AssignPlanData, AssignOptions, ModifyTermineFromOptions, clearAllDates, Load) | Alles nur von Stammdaten abhängig ⇒ in C# einmalig in unveränderlicher Plan-Definition vorberechnen; `sollGamesByDate` als 2D-Array über Wunschtag-Indizes. |
| `TMannschaftDataCache` (lazy `getCache(Plan)`) | Mannschaft | `TeamsOnDay`, `TeamIdToTeamId` (Auswärtskoppel), `CachedTeamsForForceHomeGames` | `TMannschaft.ClearCache` | Nur Stammdaten ⇒ vorberechnen, als int-Indexlisten/Bitsets. |
| `cachedGameList` | Plan | `"HeimId|GastId"` → Spiele der Paarung | neu in `GenerateGames` | String-Konkatenation + Hash im Hot-Path (`TerminIsValid`, `MarkSecondGameAsUnNecessary`, `NeuWuerfeln`) ⇒ in C# `int[,]`/`int[home*n+guest]` → Spielindizes. |
| `TAuswaertsKoppel.CachedMannschaftA/B` | Wunsch | Mannschafts-Objekt zu TeamA/B-Name | nie (neues Objekt bei Assign); `nil`-Ergebnis wird nicht gecacht | In C# beim Laden auflösen (Index). |
| `TWunschTermin.assignedGame` | Wunschtermin | Rückzeiger „Termin belegt durch" | `TGame.setDate`, `ClearMannschaftWunschTerminRefs`, `RemoveNotValidDates` | Siehe Auffälligkeit Z. 8137 (Rückzeiger wird ohne Eigentümerprüfung gelöscht). |
| `gamesRef` | Mannschaft | Nach Datum sortierte Spiele (undatierte vorne) | `GenerateMannschaftsRefGames`, `SortMannschaftsDates` nach jedem Füllen | Viele Kostenarten setzen Sortierung voraus (Koppel-Erkennung über Nachbarindex!). |

Weitere Hinweise: `{$ifdef CACHE_TEST}` erzwingt Neuberechnung und prüft in `setCachedKosten`, dass Cache-Wert = Neuberechnung (Konsistenztest – in C# als Unit-Test/Debug-Assert übernehmen). `KostenCache` wird bei `mktKoppelTermine`, `mktAuswaertsKoppelTermine`, `mktAusweichTermine`, `mkt60Kilometer` erst **nach** der „hat überhaupt Werte"-Prüfung gelesen.

---

## Persistenz in PlanTypes

### Eingabe (`TPlan.Load` aus `TPlanData`, XML-Tags aus PlanDataObjects)

- Root-Attribute: `gender`, `mid` (Beginn Rückrunde), `from`, `until`, `name`, `id`.
- `team`: `clubid`, `teamid`, `teamnumber`, `teamname`, `location`, `homerights` (HomeRightCountRoundOne); Kinder:
  - `homegameday`: `datetime`, `parallelgames`, `location`, `ausweichtermin` (bool), `couplegameday` (bool) / `doublegameday` (bool), `coupleprio` (1 = soft, 2 = hard, sonst possible), `couplesecondtime` (Zeit), `coupleauswaertssecondtime` (bool).
  - `nogameday` (`date`) → Sperrtermin; `roadcouple` (`teamnamea`, `teamnameb`, `sameday`: 0 = SameDay, 1 = DiffDay, sonst AllDay); `homerights` (`teamname`, `homeright` 1/2); `noweekgames` (`teamname`).
  - `sisterteam` (`gender`, `teamnumber`, `teamname`, `location`, `noparallelgames`, `parallelhomegames`) mit `sistergame` (`hometeamname`, `guestteamname`, `datetime`, `location`).
- Plan-Ebene: `predefinedgames/game`, `existingschedule/game` (`hometeamname`, `guestteamname`, `datetime`, `location`; Auflösung **per Teamname**), `nogameday` (`date` → FreeGameDays), `mandatorygames` (`datefrom`, `dateto`, `numbergames`), `ranking` (`active`, `team`: `teamname`, `rankingindex`).
- Datumsformate (PlanDataObjects): Datum `dd.mm.yyyy`, DatumZeit `dd.mm.yyyy hh:mm`.

### `TCalculateOptions.Save/Load` (eigene XML-Datei, Pfad vom Aufrufer)

```xml
<?xml version="1.0" encoding="iso-8859-15"?>
<andigenerator-options>
  <weighting friday-is-part-of-weekend="true|false"
             gameday="coNormal" gameday-overlapp="…" last-gameday="…" last-gameday-overlapp="…"
             sister-game-at-start="…"
             mktHalleBelegt="coNormal" … mktMandatoryGames="coNormal">   <!-- 16 Attribute, Name = Enum-Name -->
    <team teamid="…" main="coNormal" mktHalleBelegt="…" … mktMandatoryGames="…"/>   <!-- je Team in GewichtungMannschaften -->
  </weighting>
  <round-planing planing="rpBoth|rpHalfRound|rpFirstOnly|rpSecondOnly|rpCorona"/>
  <double-round active="true|false" automatic-mid-date="true|false" mid-date-1="dd.mm.yyyy" mid-date-2="dd.mm.yyyy"/>
</andigenerator-options>
```

- Werte = Delphi-Enum-Namen via RTTI (`GetEnumName/GetEnumValue`); unbekannte/fehlende Werte ⇒ `coNormal` bzw. `rpBoth`.
- `mid-date-*` nur geschrieben, wenn nicht automatisch und ≠ 0.
- Load: `Clear()`, Datei fehlt ⇒ Defaults; **jede Exception wird geschluckt** und erneut `Clear()`. Fehlt das Attribut `friday-is-part-of-weekend`, wird es `false` (Default von `Clear` wäre `true`). Fehlt `double-round` komplett, bleibt `AutomaticMidDate = true`.
- Defaults (`Clear`): Freitag erlaubt, `rpBoth`, keine Doppelrunde, automatische Mitte, alle Gewichte `coNormal`, keine Team-Gewichte.

### `SaveScheduleToCsv(FileName)` (click-TT-Importformat)

- Header (1 Zeile, Semikolon): `Nr.;Vor/Rück;Tag;;Datum;;Uhrzeit;HeimVereinNr;Heim-Mannschaft;GastVereinNr;Gast-Mannschaft;HeimMannschaftNr;GastMannschaftNr;Ergebnisse;Spiellokal`
- Pro Spiel (sortiert nach Datum, dann „Heim Gast"; nur Spiele mit `IsInRoundToGenerate`):
  `i+1` (Index über **alle** Spiele ⇒ Nummerierung hat Lücken bei Teilplanung) `;` Vor/Rück (`0`; `1` wenn Datum > `DateBeginRueckrundeInXML` und nicht Halbrunde) `;` Wochentag deutsch `;;` `dd.mm.yyyy` `;;` `FormatTimeForExport` (PlanDataObjects) `;` HeimclubID `;` Heim-teamName `;` GastclubID `;` Gast-teamName `;` Heim-teamId `;` Gast-teamId `;` (leer = Ergebnisse) `;` Location `;` (abschließendes `;`).
- Undatierte Spiele erhalten `DateBegin` als Datum (!). Location aus predefinedGame.OverrideLocation, sonst Wunschtermin-Location.
- `TStringList.SaveToFile` ohne Encoding-Angabe ⇒ Delphi-Standard (ANSI-Codepage des Systems, i. d. R. Windows-1252) – **zu verifizieren**, siehe Offene Fragen.

### `SaveScheduleToXml(FileName)` / `LoadScheduleFromXml(FileName)`

- Speichern über `TPlanData.SaveToXML` (UTF-8, Root `andigenerator`): `<schedule><game datetime="dd.mm.yyyy hh:mm" hometeamname="…" guestteamname="…"/>…</schedule>` – **ohne Location**.
- Laden: `schedule/game` per `LoadGameDates` (Teamname-Auflösung, liest auch `location` in `OverrideLocation`) → `AssignGames` (Zuordnung per teamId; OverrideLocation wird dabei **nicht** übernommen).

---

## Threading / Performance

### Thread-Sicherheit

- **Eigentum:** Jeder `TPlanCalcThread` besitzt zwei vollständige Deep-Copies: `Plan` (Arbeitskopie, nur im Thread benutzt) und `OptPlan` (beste Lösung des Threads). Alle Mannschaften, Spiele, Wunschtermine, Caches (`TPlanDataCache`, `TMannschaftDataCache`, `KostenCache`, `cachedGameList`, `TCalculateOptions`) sind pro Plan-Kopie ⇒ keine geteilten Datenstrukturen im Hot-Path.
- **Lock:** `ParentCriticalSection` (vom `TPlanMainThread` übergeben, gemeinsam für alle Worker einer Insel) schützt: Schreiben von `OptPlan`/`OptKosten`/`RetKosten`/`LastOptimizedThread` bei Verbesserung, `GetActResult` (Lesen von `OptPlan` durch den MainThread), `DoReInitPlan/DoReInitPlanData` (Übergabe neuer Pläne via `ReInitPlan`/`ReInitPlanData` + Flags) und deren Verarbeitung im Worker.
- **Ungeschützt:** `Paused` (boolean, vom MainThread geschrieben – benigne), `Durchlauf` (int64, wird in `GetActResult` unter Lock gelesen, im Worker aber ohne Lock inkrementiert – auf 64-Bit atomar, auf 32-Bit theoretisch zerrissen), `SetThreadType` wird von PlanMainThread zur Laufzeit ohne Lock aufgerufen (Z. 211 in PlanMainThread) und ändert `TauschTyp/Anzahl/Percent` während `Execute` läuft (benigne Race).
- **Globaler Zustand:** Delphi-`Random` (globaler `RandSeed`, LCG) wird aus allen Threads ohne Synchronisation benutzt (`Random` in NeuWuerfeln, FillTermineIntern, FillTerminIntern, PercentRandom, GenerateRaster). Data-Race ⇒ nicht reproduzierbar, Sequenzen korreliert, aber kein Absturz. `Randomize` in PlanMainThread/PlanOptimizer. `MinGameDistance` ist globale `var`, nur gelesen. `CacheHit/Miss` nur Debug.
- Speicher: Worker-Kopien werden über `TPlan.Assign` erzeugt (Stammdaten + Schedule über namensbasierte Zwischenliste) – teuer, aber nur bei Start/ReInit.

### Hot-Paths & Performance-Merkmale

- Pro Durchlauf: `TransferRefDateToDate` (O(Spiele)), `NeuWuerfeln`, `FillTermine` (für jedes offene Spiel zufällige Wunschtermin-Probe mit `TerminIsValid` → `IsTerminFree` linear über `gamesRef` beider Teams + zwei `findGames`-Lookups mit String-Key + `GetRoundNumberByDate` linear über Runden), `SortMannschaftsDates` (Bubble-Sort je Team), `CalculateKosten` (inkrementell dank Cache, Early-Exit).
- Allokationen im Hot-Path: `TGameList` in `FillTermineIntern`/`NeuWuerfelnForGames`/`CreateAuswaertsKoppeledGamesDependenciesList`, dynamisches `IndexList`-Array in `FillTermin` (+ Kopie als Open-Array-Wertparameter), `TMannschaftsKosten` je Team×Typ, `TList<Integer>`/`TList<Double>` in Kostenfunktionen, `TStringList`-Kopie in `GetMissingParallelHomeGames`, String-Keys in `findGames`/`getNumSollGames`.
- `inline`-Hinweise an: `setDate`, `setEmptyDate`, `DateValid`, `isTheSame`, `ClearValues`, `getGesamtKosten`, `Find/Exists`, `getMannschaftA/B`, `get/IsEmpty` (SisterGames), `IsKoppelTermin`, `get/setCachedKosten`, `ClearKostenCache`, `FindByName/TeamId`, `findGames`, `FillTermin`, `GetSpielTageKostenFaktor`, `GetMannschaftKostenFaktor`, `getCache`, `Has*`, `GetRoundNumberByDate`, `getOptions`, `NoGames`, `GetFirst/LastDate`, `DateInRound`, `FindValueByDate`.
- Kommentar Z. 7331: „75.000 Pläne pro Sekunde, alte Version" (Referenzgröße für Parallel-Spiele-Prüfung).
- Worker: `Priority := tpLower`; LowPrio-Worker (Raster, ≥30 %) schlafen 50 ms pro Durchlauf nach 10 000 Durchläufen seit ReInit; alle rufen `Sleep(0)` pro Durchlauf.

---

## Plattformabhängigkeiten

- `Windows.FillMemory` (Nullen von Arrays) → `Array.Clear`/`stackalloc`/`Span.Clear`.
- `TThread` mit `Priority := tpLower` (Windows-Threadpriorität) → .NET `ThreadPriority.BelowNormal` (auf Linux/macOS teils wirkungslos) bzw. Tasks.
- `Sleep(0)/Sleep(n)` → `Thread.Yield()`/`Thread.Sleep`.
- `Xml.XMLDoc` (MSXML-basiert unter Windows) → `System.Xml.Linq`.
- `TStringList.SaveToFile` (ANSI-Default) → explizites `Encoding`.
- `DateTimeToString`/`MyFormatDate` nutzen Locale-Formatzeichen (`:` ist Platzhalter für den System-Zeittrenner, `ddd` = lokalisierter Wochentagsname) → in C# `CultureInfo.InvariantCulture` bzw. `de-DE` explizit.
- `TypInfo`-RTTI für Enum-Namen → `Enum.GetName/Enum.TryParse` (Namen identisch halten!).
- `Graphics`, `Messages`, `Variants` in uses, aber (bis auf `VarToStr`) ohne Verwendung.

Keine UI-Abhängigkeit in dieser Unit ⇒ vollständig in eine plattformneutrale .NET-Bibliothek portierbar.

---

## Migrationshinweise für C#

### Vorgeschlagene Aufteilung

| C#-Projekt / Datei | Inhalt (aus PlanTypes) |
|---|---|
| `AndiGenerator.Core/Model/Enums.cs` | `WishOption` ([Flags]), `TeamCostType`, `PlanCostType`, `AwayCoupleType`, `HomeRightValue`, `Weight` (CalculateOptionsValues), `RoundPlanning`, `ShuffleType`, `MessageType`; Namens-/Wert-Tabellen (`cCalculateOptionsValues` …) als `static readonly`-Arrays. |
| `Core/Model/PlanDefinition.cs` | **Unveränderliche Stammdaten** (einmal geladen, von allen Threads geteilt, read-only): Teams, Wunschtermine (Tag+Minute, Options, ParallelGames, Location-Id), Sperr-/Ausweichtage (Bitset je Team über Tagnummer), noWeekGame (`bool[n,n]`), HomeRights (`sbyte[n,n]`), Auswärtskoppel (Paare + Typ, `TeamsOnDay`-Bitset), SisterGames (nach Tag indiziert, Location-Id), FreeGameDays (Bitset), MandatoryPeriods, Ranking (`rankOfTeam[]`), Rounds (+ vorberechnete Lookup-Tabellen `roundOfDay[]`, `inRoundToGenerate[]`), Spielliste (Heim/Gast-Indizes), `gamesByPair[home*n+guest]`, SollGames-Tabelle, Plan-Flags (`Has*`). |
| `Core/Model/TeamWeights.cs`, `CalculateOptions.cs` | `TGewichtungMannschaften`, `TCalculateOptions` (reine Daten + `Clone`), Faktor-Vorberechnung `float[team, costType]`. |
| `AndiGenerator.Engine/PlanSolution.cs` | **Veränderlicher Zustand pro Worker**: `int[] gameDay`, `short[] gameMinute`, `int[] gameWishIdx` (−1 = keins), `byte[] gameFlags` (Fixed, NotNecessary), `int[] wishAssignedGame`, `int[][] teamGames` (sortiert), Commit/Rollback-Kopien (Array.Copy statt *Ref-Felder). |
| `Engine/Validity.cs` | `TerminIsValid`, `IsTerminFree`, `GameIsValid`, Koppel-Erkennung. |
| `Engine/Filler.cs` | `FillTermine`, `FillTermin(Intern)`, `FillAuswaertsKoppelTermin`, `FillPredefinedGames`, `RemoveNotValidSecondTimeGames`. |
| `Engine/Shuffler.cs`, `Engine/RasterGenerator.cs` | `NeuWuerfeln*`, `GenerateRaster`, `CreateRasterOneRound`. |
| `Engine/Costs/CostCalculator.cs` + `Costs/*.cs` (ein statisches Modul je Kostenart) | `CalculateKosten` mit Early-Exit, `TeamCostCache` (struct-Array `[team, type]` + Commit-Kopie), Plan-Kosten, harte Fehler. Diagnose-/Meldungsvariante getrennt (`CostReport`), damit der Hot-Path frei von Strings bleibt. |
| `Engine/PlanWorker.cs` | `TPlanCalcThread` (Loop, eigene RNG-Instanz, ReInit-Handshake über `lock`/`Interlocked`/`volatile`). |
| `AndiGenerator.Persistence/OptionsXml.cs`, `ScheduleCsv.cs`, `ScheduleXml.cs`, `PlanLoader.cs` | `TCalculateOptions.Load/Save`, `SaveScheduleToCsv`, `SaveScheduleToXml`, `LoadScheduleFromXml`, `TPlan.Load` + `TMannschaft.load*`. |
| `Core/Diagnostics/PlanChecks.cs` | `getHardErrorMessages`, `getTermineNotPossible`, `getDiffToDefault`, `AsString`-Formatierungen. |

### Datenstrukturen für Performance

- **Datum als int-Tagnummer** (+ separate Minuten 0–1439 als `short`): Delphi-TDateTime-Seriennummer (Tag 0 = 30.12.1899) beibehalten, damit `MondayBefore = ((d−2) div 7)*7+2` und `InternalWeekNumber = (d−2) div 7` identisch bleiben; C#-`/` auf negativen Zahlen beachten (hier nur positive Tage). Exakte Double-Vergleiche (`WunschTermin.Date = Game.Date`, `Find`, `isTheSame`) werden zu Integer-Vergleichen – robuster. `MinGameDistance` = 210 Minuten, Fenster ±89 min (Halle) bzw. ±239 min (parallel).
- **Indizes statt Referenzen:** Teams `0..n−1`, Spiele `0..G−1`, Wunschtermine global indiziert; `gamesByPair` statt `"id|id"`-Dictionary; Team-Namenssuche nur beim Laden.
- **struct statt class:** `KostenValue` (readonly record struct), Cache als `KostenValue[n*16]`; `Game`-Zustand als SoA-Arrays oder `struct GameState`. Keine per-Aufruf-Objekte (`TMannschaftsKosten`) im Hot-Path.
- **Arrays statt Listen**, `Span<T>`/`stackalloc` für temporäre Index-/Wertelisten (IndexList in FillTermin, Abstände in Abstand-H/A, Spielverteilung), `ArrayPool` für größere.
- **Bitsets** (`ulong[]`) für Sperrtage, Ausweichtage, freie Tage, Tage mit Spiel je Team (O(1)-Konfliktprüfung in `IsTerminFree`).
- **Deep-Copy** von Worker-Plänen: nur `PlanSolution` kopieren (Array.Copy), Stammdaten teilen ⇒ ReInit/AssignOptimizedDates werden Mikrosekunden statt Millisekunden.
- **Sortierung** der Team-Spiellisten: Insertion-Sort auf kleinen `int[]` mit identischem Tie-Break (Datum, dann ordinaler Vergleich „Heimname Gastname" ⇒ vorab als Rang-Int pro Spiel berechnen). Undatierte Spiele (Tag 0) müssen weiterhin **vorne** stehen (ttSpielTag, Koppel-Nachbarschaft hängen davon ab).
- **RNG:** pro Worker eine eigene Instanz (`Random` mit Seed oder Xoshiro256**), optional deterministischer Modus für Tests. Globales geteiltes `Random` wäre in .NET **nicht** threadsicher (kann dauerhaft 0 liefern) – `Random.Shared` ist threadsicher, aber für Reproduzierbarkeit eigene Instanzen.
- `Random(n)` in Delphi = `Trunc(n * Zufall[0,1))` → `rng.Next(n)`; `min(Count−1, Random(Count))`-Absicherungen können entfallen.

### Mapping

`TList<T>/TObjectList<T>` → `List<T>`/Arrays; `TDictionary` → `Dictionary`; `set of TWunschterminOption` → `[Flags] enum WishOption : ushort`; `TDateTime` → `int day` + `short minute` (bzw. `DateTime` nur an IO-Grenzen); `TStrings` → `List<string>`; `TCriticalSection` → `lock`/`Monitor`; `TThread` → dedizierter `Thread` (lange Laufzeit, BelowNormal) oder `Task` mit `LongRunning`; `Power(x,y)` → `Math.Pow` (für ganzzahlige kleine Exponenten `x*x*x` – Ergebnis bitgleich nicht garantiert, fachlich irrelevant); `Trunc` → `(int)`/`Math.Truncate`; `compareStr` → `string.CompareOrdinal`; `DayOfWeek` (1 = Sonntag … 7 = Samstag) → `.NET DayOfWeek` (0 = Sonntag) bzw. `(day+6)%7`-Arithmetik; RTTI-Enum-Namen → `Enum.GetName`/`Enum.TryParse(ignoreCase:false)` mit **gleichen Bezeichnern** (`coNormal`, `mktHalleBelegt`, `rpCorona` …) wegen Dateikompatibilität.

### Fallstricke

- `TDateTime` als Double: Termine mit Uhrzeit werden an vielen Stellen exakt verglichen; `FixDateTime` korrigiert Rundungsfehler beim Laden. Mit int-Minuten entfällt das, aber Import muss identisch runden (auf Minute).
- `Trunc(Date)` auf Tag; `RoundInfo` halboffen `[From, To)` mit Uhrzeitanteil – z. B. `DateBeginRueckrundeInXML` ist Tagesanfang ⇒ Spiel am Rückrundenstarttag gehört zur Rückrunde; im CSV aber `Date > DateBeginRueckrundeInXML` ⇒ ebenfalls Rückrunde (weil Uhrzeit > 0), Spiel um 00:00 wäre Vorrunde.
- Reihenfolge der Kostenblöcke und Early-Exit-Punkte beibehalten (sonst andere Performance, aber gleiche Ergebnisse); Akzeptanz strikt `<`.
- `gamesRef`-Sortierung mit undatierten Spielen vorne und Koppel-Erkennung über Nachbarindex sind **fachlich relevant** (z. B. `getKoppeledGameAfter` rekursiv, keine 3er-Koppel).
- 1-basierte Strings nur in `SetThreadType` (`TauschArt[1]`, `Copy(…, 4, …)`).
- Locale: `MyFormatDate` („ddd dd.mm.yyyy“ ⇒ deutscher Kurztag nur bei deutscher Windows-Locale), `FormatDateForExport` mit `/`-freier Maske; CSV-Dezimal-/Zeittrenner explizit festlegen.
- Owner-Semantik: `gamesAllowed`, `predefinedGames`, `existingSchedule`, `gamesNotAllowed` besitzen ihre Spiele; `gamesRef`, `cachedGameList`-Listen, `SisterTeamsInPlan` nicht.

### Aufwandsschätzung

**XL** – ~9 500 Zeilen dicht verzahnter Fachlogik (16+5 Kostenarten mit empirischen Formeln, Koppel-/Rundenlogik, inkrementeller Cache), deren Ergebnisse numerisch vergleichbar bleiben müssen; zusätzlich Umbau auf daten-orientierte Strukturen und umfangreiche Regressionstests (Referenzpläne aus Delphi gegen C#-Kostenwerte).

### Bugs / Auffälligkeiten (bewusst entscheiden)

1. **Z. 8137–8140 `TGame.setDate`:** `FWunschTermin.assignedGame := nil` ohne Prüfung, ob `assignedGame = Self`. In `TransferRefDateToDate` kann ein bereits von Spiel B (Ref-Zustand) belegter Wunschtermin X wieder auf `nil` gesetzt werden, wenn später Spiel A (aktuell auf X) auf seinen Ref-Termin zurückgesetzt wird. X gilt dann als frei; `IsTerminFree` verhindert i. d. R. trotzdem Doppelbelegung (gleiches Heimteam, gleicher Tag), aber der Zustand ist inkonsistent. C#: nur löschen, wenn Eigentümer.
2. **Z. 6120/6157 `CalculateKostenAuswaertsKoppelTermine`:** `array[0..MAX_MANNSCHAFT(30)]` ohne Größenprüfung; >31 Teams ⇒ Speicherüberschreibung (ohne Range-Check). `mktAbstandHeimAuswaerts` (Z. 5235) wirft bei >30 Teams eine Exception ⇒ faktisch Limit 30 Teams je Liga. `MAX_ROUND = 10`; >9 Spiele gegen denselben Gegner werden ignoriert.
3. **Z. 6509 `mktSpielverteilung`:** Division durch `gamesRef.Count` (0 Spiele ⇒ 0/0 ⇒ Delphi EInvalidOp bzw. NaN).
4. **Z. 1436/1395 `CreateRasterOneRound`:** Bei **ungerader** Teamzahl werden nur `n−1` Spieltage/Raster-Indizes verwendet, obwohl das Kreisverfahren `n` Runden braucht ⇒ ein Raster-Spieltag fehlt; die Spiele werden später zufällig von `FillTermine` gesetzt. Parameter heißt `RueckRunde`, ist aber für Runde 0 `True` (nur Alternierung, Namensverwirrung). Alle Runden nutzen dieselbe `SpieltagIndexe`-Permutation (gespiegelte Runden).
5. **Z. 4695–4707 `FillTermineIntern`:** Über den 70-%-Auswärtskoppel-Pfad kann auch ein **bereits datiertes** Spiel an `FillTerminIntern` übergeben werden; ebenso werden Einträge in `ListOpenGames` nicht erneut auf `Date=0` geprüft. Meist wirkungslos (Selbstkonflikt in `TerminIsValid`), bei Doppelrunde kann ein Spiel aber in eine andere Hauptrunde „umziehen".
6. **Z. 3944 `LastKostenOverlap`:** wird in der Schleife überschrieben statt summiert (Wert des ältesten überlappenden Spieltags).
7. **Z. 5980/6060 `mktKoppelTermine`:** `AllCount` wird zweimal halbiert und dann überschrieben (nur Anzeige, harmlos).
8. **Z. 8562 `TCalculateOptions.Load`:** fehlendes Attribut `friday-is-part-of-weekend` ⇒ `false` (Default sonst `true`); alle Ladefehler werden stumm verschluckt.
9. **Z. 2585/2603 CSV:** Nummerierung `i+1` über alle Spiele (Lücken), undatierte Spiele werden mit `DateBegin` exportiert statt ausgelassen/markiert.
10. **Z. 2668 `SaveScheduleToXml`:** Location wird nicht gespeichert; `LoadScheduleFromXml` → `AssignGames` übernimmt geladene `OverrideLocation` nicht.
11. **Z. 8909 `ToString`:** `SisterTeamsAmAnfangGewichtung` wird doppelt ausgegeben, `GewichtungMannschaften` gar nicht – nur für Trace, kosmetisch.
12. **Z. 1840 Kommentar** in `ModifyTermineFromOptions` falsch (betrifft Sperrtermine, nicht Auswärtskoppel).
13. **Z. 901** lokale `function Date: TObject` (Rückgabe nil) überdeckt `SysUtils.Date` in der Unit – toter Code, nicht portieren.
14. **Z. 4292** Annahme „ohne Koppel sind alle Termine gültig" (`AllDatesAreValid`): harte „nicht erlaubt"-Kosten werden dann nicht geprüft – bei Portierung beibehalten (Performance) oder per Debug-Assert absichern.
15. **Z. 1577/1596** `ttMannschaft`/`ttSpielTag` ziehen mit Zurücklegen ⇒ Duplikate in der Tauschliste (verringert effektive Tauschmenge, harmlos).
16. **Datenrennen** auf globalem `Random` (alle Threads) und `SetThreadType` ohne Lock (PlanMainThread Z. 211).

---

## Offene Fragen

1. Soll das C#-Ergebnis **numerisch identische Kosten** wie Delphi liefern (Regressionstest gegen Referenzpläne) oder genügt fachliche Gleichwertigkeit? (Beeinflusst, ob Formeln inkl. Eigenheiten wie Punkt 6/7 1:1 übernommen werden.)
2. Welches **Encoding** erwartet click-TT beim CSV-Import (Delphi schreibt vermutlich ANSI/Windows-1252)? Soll ein BOM geschrieben werden?
3. Grenze **30 Mannschaften** pro Liga beibehalten oder aufheben (Arrays dynamisch)?
4. Bug 1 (`assignedGame`-Rückzeiger) und Bug 5 (bereits datierte Spiele im Auswärtskoppel-Pfad) korrigieren? Beides kann die Suchdynamik minimal verändern.
5. Raster bei ungerader Teamzahl (Bug 4): Originalverhalten beibehalten oder korrektes Kreisverfahren mit `n` Runden?
6. Soll die Optimierung **reproduzierbar** (Seed pro Worker) sein, z. B. für Tests/Support?
7. CSV-Export undatierter Spiele mit `DateBegin` – gewollt (click-TT verlangt Datum?) oder Fehler?
8. `TPlanNoGameDay(List)` und `TMannschaft.KostenAnzahl/Abstand/Wochentag` werden hier nicht genutzt – noch in UI/anderen Units relevant oder Altlast?
9. Optionsdatei weiterhin `iso-8859-15` schreiben (Kompatibilität mit bestehenden Installationen) oder auf UTF-8 umstellen (Lesen beider Varianten)?
