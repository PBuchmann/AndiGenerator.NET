# DialogOptions (DialogOptions.pas + DialogOptions.dfm)

**Kategorie:** UI-Dialog (nicht-modales Werkzeugfenster „Einstellungen“)
**Umfang:** 539 Zeilen .pas / 267 Zeilen .dfm
**Abhängigkeiten (uses):** PlanTypes (`TPlan`, `TCalculateOptions`, `TMannschaftsKostenType`, `TPlanKostenType`, `TCalculateOptionsValues`, `TRoundPlaning`, Namenskonstanten), PlanUtils (`FixFormSize`, `FixControls`, `StartWaitCursor`), DialogPanelOptionArray (`TFormPanelOptionArray`), DialogForSingleGewichtungsOptions (`TDialogForSingleGewichtungsOptions`), EditMandatoryDatesDialog (**ungenutzt**); VCL: Forms, ComCtrls (TPageControl, TDateTimePicker), ExtCtrls (TTimer, TBevel), StdCtrls, Generics.Collections
**Verwendet von:** AndiGeneratorMain (`FormOptions: TFormOptions`, `ToolButtonOptionsClick`, `DoOnAfterPlanChanged`, Zeilen 89, 459–461, 899–901, 1094–1100)

## Zweck
Einstellungsfenster für die **Gewichtung der Kostenarten** der Optimierung (Plan-Kosten und Mannschafts-Kosten, global und je Mannschaft), die **60-km-Freitagsregel** sowie die **Rundenplanung** (Vor-/Rückrunde, Halbrunde, Doppelrunde mit Viertelrunden-Stichtagen, „Corona-Runde“). Das Fenster ist **nicht modal** und übernimmt **jede Änderung sofort** („Live-Apply“): nach jeder Control-Änderung werden die Optionen in `Plan.getOptions()` geschrieben und über `OnAfterSave(Self, True)` im Hauptfenster gespeichert, der Plan neu geladen und der laufende Optimizer mit den neuen Gewichten versorgt.

## Inhalt / Struktur

### Typen
- `TOnSaveEvent = procedure(Sender: TObject; SaveOnlyOptions: boolean) of object` – Callback ans Hauptfenster (dort `DoOnAfterPlanChanged`).
- `TFormOptions = class(TForm)` – das Fenster.
  - Private: `Plan: TPlan` (Referenz auf `PlanLoaded` des Hauptfensters, **keine Kopie**), `FOnAfterSave`, `InLoad: boolean` (Reentranz-/Ladeschutz), `PanelPlanOptions`, `PanelMannschaftOptions: TFormPanelOptionArray` (eingebettete Listen „Titel + ComboBox mit 7 Stufen“).
- `TSubDialogController = class` (nur im implementation-Teil) – Controller für den Unterdialog „Mannschaften anpassen…“; besitzt eine **eigene TPlan-Kopie** (`Plan := TPlan.Create`, im Destruktor freigegeben).

### UI-Elemente (.dfm)
Fenster: Caption „Einstellungen“, `BorderStyle = bsSizeToolWin`, Größe via `FixFormSize(Self, 848, 697)`, `Position = poMainFormCenter`.
- **PageControl** (`OnChange = PageControlChange` → `EnableButtons`)
  - **TabSheetGewichtung** „Gewichtung der Kosten“
    - `CheckBoxFreitagIs60km` „Freitagspiele sind bei 60km Regel zulässig“ (`OnClick = ControlChanged`)
    - `GroupBox1` „ Allgemeine Plankosten “ → `PanelPlanGewichtung` (Host für `PanelPlanOptions`)
    - `GroupBox2` „ Mannschaftskosten “ → `PanelMannschaftenGewichtung` (Host für `PanelMannschaftOptions`) + Button `ButtonGewichtungMannschaften` „Mannschaften anpassen…“
  - **TabSheetDoppelrunde** „Runden“
    - `ComboBoxPlanRounds` (DropDownList, `OnChange = ComboBoxPlanRoundsChange`), Einträge in Enum-Reihenfolge `TRoundPlaning`:
      0 `rpBoth` „Vor- und Rückrunde planen“, 1 `rpHalfRound` „Halbrunde planen“, 2 `rpFirstOnly` „Nur Vorrunde planen“, 3 `rpSecondOnly` „Nur Rückrunde planen“, 4 `rpCorona` „Coronarunde planen (Nur die nicht gemachten Spiele in der Rückrunde planen)“
    - `CheckBoxDoubleRound` „Doppelrunde planen“ (`ControlChanged`)
    - `CheckBoxDateAutomatic` „Endedatum der Viertelrunden automatisch ermitteln“ (`CheckBoxDateAutomaticClick`)
    - `LabelFirstRoundDate` „Endedatum der 1. Viertelrunde“ + `DateTimeMid1` (TDateTimePicker, `ControlChanged`)
    - `LabelThirdRoundDate` „Endedatum der 3. Viertelrunde“ + `DateTimeMid2` (TDateTimePicker, `ControlChanged`)
- **PanelBottom**: `ButtonToStandard` „Standard wiederherstellen“, `ButtonOK` „Schließen“ (setzt nur `Visible := False`).
- **Timer1** (TTimer, Standard-Intervall 1000 ms, aktiv) → `EnableButtons` jede Sekunde (Polling).

### Kostenarten (aus PlanTypes, hier angezeigt)
**Plan-Kosten** `TPlanKostenType` (Reihenfolge = Anzeigereihenfolge, `cPlanKostenTypeNamen`):
| Index | Enum | Anzeige | Feld in TCalculateOptions | XML-Attribut (`weighting`) |
|---|---|---|---|---|
| 0 | pktGameDayOverlap | Überlappung der Spieltage | `SpieltagOverlapp` | `gameday-overlapp` |
| 1 | pktGameDayLength | Länge der Spieltage | `SpieltagGewichtung` | `gameday` |
| 2 | pktLastGameDayOverlap | Überlappung letzer Spieltag | `LastSpieltagOverlapp` | `last-gameday-overlapp` |
| 3 | pktLastGameDayLength | Länge letzer Spieltag | `LastSpieltagGewichtung` | `last-gameday` |
| 4 | pktSisterGamesAtBegin | Vereinsinterne Spiele am Anfang | `SisterTeamsAmAnfangGewichtung` | `sister-game-at-start` |

**Mannschafts-Kosten** `TMannschaftsKostenType` (16 Werte, `cMannschaftsKostenTypeNamen`), gespeichert in `GewichtungMainMannschaftsKosten[]`, XML-Attributname = Enum-Name:
`mktHalleBelegt` Hallenbelegung · `mktSisterGames` parallele Spiele · `mktForceSameHomeGames` synchrone Heimsp. · `mktSperrTermine` Sperrtermine · `mktAusweichTermine` Ausweichtermine · `mkt60Kilometer` 60km Regel · `mktEngeTermine` 3-Tage-Abstand · `mkt2SpieleProWoche` 2-Spiele-die-Woche · `mktSpielverteilung` Spielverteilung · `mktKoppelTermine` Heimkoppel · `mktAuswaertsKoppelTermine` Auswärtskoppel · `mktZahlHeimSpielTermine` Ungleich H/A · `mktWechselHeimAuswaerts` Wechsel H/A · `mktAbstandHeimAuswaerts` Abstand H/A · `mktRanking` Setzliste · `mktMandatoryGames` Pflichtspieltage.

**Gewichtungsstufen** `TCalculateOptionsValues` (`cCalculateOptionsNamen` / `cCalculateOptionsValues` / `cCalculateOptionsDisplayInteger`):
| Enum | Anzeige | Faktor (×/100) | Display-Integer |
|---|---|---|---|
| coIgnore | Nicht berücksichtigen | 0 | −1000 |
| coSehrWenig | sehr wenig | 1 | −2 |
| coWenig | wenig | 10 | −1 |
| coNormal | normal (Default) | 100 | 0 |
| coHoch | hoch | 1000 | 1 |
| coSehrHoch | sehr hoch | 10000 | 2 |
| coExtremHoch | extrem hoch | 100000 | 3 |

### Methoden
- `FormCreate` – `FixFormSize(848, 697)`, erste Seite aktiv, erzeugt die beiden `TFormPanelOptionArray`, hängt sie per `SetPanel` in die Host-Panels, setzt `OnChanged := ControlChanged`, initialisiert Titel aus den Namenskonstanten (`Init(titles)`), `FixControls`.
- `SetValues(Plan)` – merkt Referenz, `OptionsToForm(Plan.getOptions())`. Wird vom Hauptfenster beim ersten Öffnen und nach jedem `DoOnAfterPlanChanged`/Dateiladen erneut aufgerufen.
- `OptionsToForm(Options)` – unter `InLoad := True`: Checkboxen, `ComboBoxPlanRounds.ItemIndex := Ord(RoundPlaning)`, Datumsfelder über `Options.getMidDate1/2(Plan)` (liefert automatisches Datum, wenn `AutomaticMidDate`), Plan-Gewichte in Enum-Reihenfolge, 16 Mannschaftsgewichte. Danach `EnableButtons`.
- `FormToOptions(Options)` – Rückrichtung; Plan-Gewichte werden über das Array `PlanGewichtung[TPlanKostenType]` den Feldern zugeordnet; `MidDate1/2 := DateTimeMid1/2.DateTime` **immer** (auch bei Automatik). Ruft am Ende `EnableButtons`. Die lokal erzeugte `EmptyOptions` ist ungenutzt.
- `ControlChanged` / `ComboBoxPlanRoundsChange` (identisch) – wenn nicht `InLoad`: `EnableButtons; Save`.
- `Save` – `StartWaitCursor; FormToOptions(Plan.getOptions()); OnAfterSave(Self, True)`.
- `CheckBoxDateAutomaticClick` – bei Aktivierung Datumsfelder auf `getAutomaticMidDate1/2(Plan)` setzen, dann `ControlChanged`.
- `EnableButtons` – (a) Enable-Logik der Rundenseite: `CheckBoxDateAutomatic.Enabled := DoubleRound`; Datumslabels/Picker nur aktiv bei `DoubleRound and not DateAutomatic`. (b) „Standard wiederherstellen“ aktiv, wenn auf der **aktiven Seite** etwas vom Standard abweicht (Gewichtung: Freitag≠True oder ein Wert≠coNormal; Runden: ItemIndex≠0, DoubleRound≠False, DateAutomatic≠True). (c) **Sichtbarkeit** der Mannschaftskosten-Zeilen: `Plan.HasValuesForType(kt)` – ausgeblendet werden z.B. 60km (keine 60km-Daten), Heimkoppel, Ausweichtermine, Auswärtskoppel, Pflichtspieltage, Setzliste, synchrone Heimspiele, wenn der Plan dafür keine Daten hat.
- `ButtonToStandardClick` – seitenabhängig: Gewichtung → Freitag=True, alle Plan- und Mannschaftsgewichte = coNormal, **`Plan.getOptions().GewichtungMannschaften.Clear`** (auch die mannschaftsspezifischen Gewichte), `ControlChanged`. Runden → ItemIndex 0, DoubleRound False, DateAutomatic True, `CheckBoxDateAutomaticClick(nil)`.
- `ButtonGewichtungMannschaftenClick` – `TSubDialogController` mit Kopie des Plans; bei OK: `Plan.getOptions().Assign(Controller.Plan.getOptions())`, `ControlChanged(nil)`.
- `ButtonOKClick` – nur ausblenden (Instanz bleibt im Hauptfenster erhalten).
- `Timer1Timer`, `PageControlChange` → `EnableButtons`.
- `ListViewFreeDatesChange`, `ListViewAusweichtermineChange` – **tote Handler** (keine entsprechenden Controls in der .dfm; Überbleibsel früherer Tabs).

### TSubDialogController (Mannschaftsspezifische Gewichtung, zweistufig)
- `Execute` – öffnet `TDialogForSingleGewichtungsOptions` mit Titel „Gewichtung der Mannschaften“, `ShowButtons := True`, je Mannschaft (in `Plan.Mannschaften`-Reihenfolge) eine Zeile mit `GewichtungMannschaften.getMain(teamId)`. Bei OK: `addMain(teamId, value)` für alle Mannschaften → Result True.
- `DoButtonClick(Index)` – Detail-Button je Mannschaft: zweiter Dialog „Gewichtung für: <teamName>“ mit 16 Kostenarten (`getDetail(teamId, kt)`), Sichtbarkeit wie oben (`HasValuesForType`); bei OK sofort `addDetail(...)` in die **Controller-Kopie** (wird nur übernommen, wenn der äußere Dialog mit OK bestätigt wird).
- Schlüssel ist die **teamId** (Click-TT-ID), nicht der Teamname.

## Fachliche Logik / Regeln
- **Effektiver Kostenfaktor je Mannschaft und Kostenart** (`TPlan.GetMannschaftKostenFaktor`, PlanTypes):
  `Faktor = 100 · f(GewichtungMainMannschaftsKosten[kt]) · f(GewichtungMannschaften.getMain(teamId)) · f(getDetail(teamId, kt))` mit `f(v) = cCalculateOptionsValues[v]/100`, `f(coIgnore) = 0`. Die drei Ebenen (global je Kostenart, je Mannschaft, je Mannschaft×Kostenart) **multiplizieren** sich; jede Stufe ist ein Faktor 10.
  Anzeige-Integer (`GetMannschaftKostenDisplayInteger`) = Summe der Display-Integer; ein `coIgnore` auf irgendeiner Ebene → −1000 (Kostenart ignoriert).
- **Plan-Kosten** (Spieltagslänge/-überlappung, letzter Spieltag, vereinsinterne Spiele am Anfang) werden mit `Multiply` analog gewichtet.
- **60km-Regel/Freitag:** `FreitagIsAllowedBy60km = True` (Standard) → Freitag zählt für die 60-km-Regel zum Wochenende (vgl. PlanTypes ~Z. 2478).
- **Rundenplanung** `TRoundPlaning`: steuert `TPlan.IsInRoundToGenerate` (welche Spiele neu geplant werden). Bei `DoubleRound` wird jede Halbserie in zwei Viertelrunden geteilt; Stichtage `MidDate1` (Ende 1. Viertelrunde) und `MidDate2` (Ende 3. Viertelrunde).
- **Automatische Stichtage** (`getAutomaticMidDate1/2`): Mitte zwischen erstem Datum und `DateBeginRueckrundeInXML` (bei `rpHalfRound`: `DateBegin..DateEnd`) bzw. zwischen Rückrundenbeginn und letztem Datum; falls >2 Spieltags-Montage existieren, wird der mittlere Spieltags-Montag (`Count div 2`) genommen.
- **Defaults** (`TCalculateOptions.Clear`): Freitag=True, `rpBoth`, DoubleRound=False, AutomaticMidDate=True, MidDate1/2=0, alle Gewichte `coNormal`, keine Mannschaftsgewichte.

## Daten & Persistenz
Die Unit selbst schreibt keine Dateien. Über `OnAfterSave` → `TFormAndiGeneratorMain.DoOnAfterPlanChanged(Sender, SaveOnlyOptions=True)` wird `PlanLoaded.getOptions().Save(PlanLoaded, CurrentPlanDirectory + '\AndiGenerator.options')` ausgeführt (**zweimal** pro Änderung, Z. 425 und 437 in AndiGeneratorMain). Format (`TCalculateOptions.Save/Load`, PlanTypes ~Z. 8540–8745), Encoding `iso-8859-15`:
```xml
<andigenerator-options>
  <weighting friday-is-part-of-weekend="true|false"
             gameday="coNormal" gameday-overlapp="…" last-gameday="…" last-gameday-overlapp="…" sister-game-at-start="…"
             mktHalleBelegt="coNormal" … mktMandatoryGames="coNormal">
    <team teamid="…" main="coNormal" mktHalleBelegt="…" … />   <!-- je Mannschaft mit Sondergewicht -->
  </weighting>
  <round-planing planing="rpBoth|rpHalfRound|rpFirstOnly|rpSecondOnly|rpCorona"/>
  <double-round active="true|false" automatic-mid-date="true|false" mid-date-1="…" mid-date-2="…"/>  <!-- mid-dates nur wenn nicht automatisch und ≠0 -->
</andigenerator-options>
```
Werte werden per RTTI (`GetEnumName`/`GetEnumValue`) als Enum-Bezeichner gespeichert → Enum-Namen sind Teil des Dateiformats. Unbekannte Werte → `coNormal`. Bei Ladefehler → `Clear` (Defaults).

## Threading / Performance
- Läuft im UI-Thread, aber **jede einzelne Änderung** (jeder Klick, jede Combo-Auswahl, jede Datumsänderung im Picker) löst aus: Optionen speichern (2× XML), `PlanLoaded.Load(PlanData)`, `PlanOptimized.AssignPlanData`, `InitPlanMainData`, `Optimizer.setNewPlanData(PlanOptimized)` (Übergabe an laufende Optimierungs-Threads), `CalculateKosten(-1)`, Neuzeichnen. Das ist teuer und kann den Optimizer stören.
- Programmatisches Setzen von `TCheckBox.Checked` feuert in VCL `OnClick` → `ButtonToStandardClick` löst mehrere aufeinanderfolgende `Save`-Zyklen aus (z.B. `CheckBoxFreitagIs60km.Checked := True` speichert, bevor die Gewichte zurückgesetzt sind).
- `Timer1` pollt jede Sekunde `EnableButtons` (inkl. `HasValuesForType` für 16 Kostenarten und Neuaufbau der Sichtbarkeit).
- Reentranz: `Save` → `DoOnAfterPlanChanged` → `FormOptions.SetValues` → `OptionsToForm` (durch `InLoad` gegen Rekursion geschützt).

## Plattformabhängigkeiten
- VCL: `TPageControl`, `TDateTimePicker` (Win32 Common Control), `TTimer`, `TBevel`, `FixFormSize/FixControls`, `StartWaitCursor` (Sanduhr-Cursor).
- Pfad mit Backslash `'\AndiGenerator.options'` (im Hauptfenster) → in C# `Path.Combine`.
- XML via MSXML (`TXMLDocument`) im Options-Save/Load.
- Datumsformat beim Speichern über `FormatDateForExport`/`DateFromString` (fest „dd.mm.yyyy“-Parsing) – locale-unabhängig halten.

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.Core.Options.CalculateOptions` (Datenklasse + XML-Serializer, kompatibel zum bestehenden Format), `AndiGenerator.UI.ViewModels.OptionsViewModel` + `OptionsView` (Avalonia, nicht-modales Fenster oder Seitenleiste), `TeamWeightingViewModel` für den Unterdialog.
- **Mapping:**
  - `TCalculateOptionsValues` → `enum Weight { Ignore, SehrWenig, Wenig, Normal, Hoch, SehrHoch, ExtremHoch }` mit Extension `Factor()` (0,1,10,100,1000,10000,100000 /100) und `DisplayInt()`.
  - `TMannschaftsKostenArray` → `Weight[]` fester Länge 16 bzw. `Dictionary<TeamCostType, Weight>`; `TGewichtungMannschaften` → `Dictionary<string teamId, TeamWeight { Weight Main; Weight[] Detail }>`.
  - Namen beim Serialisieren exakt wie Delphi-Enum-Namen (`"coNormal"`, `"mktHalleBelegt"`, `"rpCorona"`) schreiben, sonst sind alte `.options`-Dateien inkompatibel.
  - `TDateTimePicker` → `DatePicker` (`DateOnly`); `TDateTime` → `DateOnly` für MidDate1/2 (Delphi speichert einen Zeitanteil aus der .dfm mit, `Time = 0.4177…`; beim Export wird nur das Datum geschrieben).
  - Live-Apply: statt sofortigem Speichern bei jedem Event ein **debounced** `OptionsChanged`-Event (z.B. 300–500 ms) bzw. `IObservable.Throttle`; Optimizer-Update als Nachricht an den Engine-Service (thread-safe Snapshot der Options übergeben, niemals geteilte mutable Instanz).
  - `Timer1`-Polling durch berechnete Properties (`CanResetToDefault`, `IsVisible` je Kostenart) + `INotifyPropertyChanged` ersetzen.
  - `InLoad`-Flag → im ViewModel über Unterdrückungs-Scope oder durch Trennung von Laden (Property-Set ohne Command) und Benutzeraktion.
- **Fallstricke:**
  - Reihenfolge der Werte in `OptionsToForm` ist hart an die Enum-Reihenfolge von `TPlanKostenType` gekoppelt (aktuell korrekt: Overlap, Length, LastOverlap, LastLength, Sister) – in C# über Dictionary/Enum statt Positionsliste lösen.
  - `ButtonToStandard` auf der Gewichtungsseite löscht auch die mannschaftsspezifischen Gewichte, aber `EnableButtons` berücksichtigt `GewichtungMannschaften` **nicht** → der Button ist ausgegraut, obwohl nur Mannschaftsgewichte abweichen (Auffälligkeit, Z. 207–229 vs. 148).
  - `MidDate1/2` werden auch bei aktivem Automatik-Modus aus den Pickern übernommen (Z. 312–313); relevant nur für den Laufzeitzustand, gespeichert wird nur bei manuell.
  - Das Fenster arbeitet direkt auf `PlanLoaded` (keine Kopie) – ein „Abbrechen“ gibt es nicht.
- **Aufwand:** M – überschaubare UI, aber Live-Apply-Kopplung an Optimizer, Unterdialog mit zwei Ebenen und kompatible XML-Serialisierung müssen sauber umgesetzt werden.
- **Bugs/Auffälligkeiten:**
  - Z. 345–355: tote Event-Handler (`ListViewFreeDatesChange`, `ListViewAusweichtermineChange`).
  - Z. 307/337: `EmptyOptions` erzeugt und freigegeben, nie benutzt; `EditMandatoryDatesDialog` in uses ungenutzt.
  - Z. 124–160: `ButtonToStandardClick` löst durch programmatische `Checked`-Zuweisungen mehrere Save-/Reload-Zyklen aus.
  - `EnableButtons` ignoriert mannschaftsspezifische Gewichte (s.o.).
  - Doppelter Options-Save pro Änderung im Hauptfenster (AndiGeneratorMain Z. 425 und 437).

## Offene Fragen
- Soll das Live-Apply-Verhalten (sofortige Übernahme in den laufenden Optimizer) beibehalten werden oder ein explizites „Übernehmen“ eingeführt werden?
- Soll „Standard wiederherstellen“ künftig auch bei nur abweichenden Mannschaftsgewichten aktiv sein?
- Genaue Semantik von `rpCorona` und `rpHalfRound` in der Engine (in PlanTypes zu dokumentieren) – hier nur UI-seitig erfasst.
- Speicherort `AndiGenerator.options` im Planverzeichnis (gilt für alle Pläne dieses Ordners) – beibehalten?
