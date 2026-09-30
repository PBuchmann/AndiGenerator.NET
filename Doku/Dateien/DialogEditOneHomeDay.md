# DialogEditOneHomeDay (DialogEditOneHomeDay.pas + DialogEditOneHomeDay.dfm)

**Kategorie:** UI-Dialog
**Umfang:** 301 Zeilen .pas / 295 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `PlanTypes`, `PlanUtils` (`FixControls`, `FixDateTime`, `GetListComboValue`, `SetListComboValue`, `FormatDateForExport`), `PlanDataObjects` (`THomeDay`, `TPlanData`), `DialogPanelHomeDaysDetail` (nur im uses, nicht benutzt); VCL: `Forms`, `StdCtrls`, `ComCtrls` (`TDateTimePicker`), `ExtCtrls`, `Samples.Spin`, `Dialogs`
**Verwendet von:** `DialogPanelHomeDaysDetail` (Doppelklick/Bearbeiten einer Kalenderzelle, Z. ~183)

## Zweck
Zentraler Editor für **einen Kalendertag** einer Mannschaft in der Wunschtermin-Ansicht (Heimspieltermine). Legt fest, ob an diesem Tag kein Heimspiel, ein Heimspiel (mit Uhrzeit, Halle, Koppel-/Doppelspieltag-Optionen, Auswärtskoppel-Alternativzeit) oder ein Sperrtermin gilt. Ergebnis ist ein `THomeDay`-Objekt, das der Aufrufer per `AsShortString` in die Grid-Zelle schreibt.

## Inhalt / Struktur
- `TDialogEditOneHomeDay = class(TForm)`, privat `Value: THomeDay` (in `FormCreate` erzeugt, in `FormDestroy` freigegeben).
- `SetValue(PlanData, TeamName, Value: THomeDay, Date)`:
  - `LabelDate` = „Datum: " + `FormatDateForExport(Date)`; kopiert `Value` via `Assign` (Arbeitskopie).
  - `ComboLocation` ← `getTeamLocationsInt` ('' + '1'..'5').
  - Modus-Combo: `NoDate` → 0, `SperrTermin` → 2, sonst 1 mit Uhrzeit, MaxParallelGame, Ausweich, Lokal; Koppel: `KoppelDate` → 1 (+ zweite Zeit, Prio), `DoubleDate` → 2 (+ Prio), sonst ggf. `KoppelAuswaertsSecondTime` → Checkbox + Alternativzeit.
  - `TeamName` wird nicht verwendet.
- `GetValue: THomeDay` – liefert die interne Arbeitskopie (Besitz bleibt beim Dialog! Aufrufer muss vor `Free` auslesen).
- `ButtonOKClick` – Form→Value (s.u.), dann `CheckValueData`; nur bei Erfolg `mrOk`.
- `CheckValueData` – Validierungen (s.u.).
- `EnableButtons` – UI-Zustandslogik (Sichtbarkeit/Enabled), aufgerufen von allen Combo-/Checkbox-Change-Events.

UI (.dfm, Caption „Wunschtermin", 645×508):
- `ComboBox` (Modus): 0 „kein Heimspiel, Auswärtsspiele möglich", 1 „Heimspiel möglich", 2 „Sperrtermin".
- `PanelHomeDayData` (nur sichtbar bei Modus 1):
  - Uhrzeit (`TimePicker`), Ausweichtermin (`CheckBoxAusweich` „nur im Notfall einplanen").
  - GroupBox „Halle": `ComboBoxMaxGames` „Maximale Heimspiele" (0 = „keine Beschränkung", 1..10), `ComboLocation` „Spiellokal" (leer = Standardspiellokal).
  - GroupBox „Heimkoppel/Doppelspieltag": `ComboBoxKoppel` (0 „kein", 1 „Koppelspieltag (zwei Spiele an einem Tag)", 2 „Doppelspieltag (zwei Spiele am Wochenende)"), `ComboBoxPrio` „Priorität bei Koppel" (0 „möglich", 1 „normal", 2 „hoch"), `TimePickerKoppelTime` „Startzeit zweites Spiel".
  - GroupBox „Auswärtskoppeloptionen": `CheckBoxAuswaertskoppel` „Startzeit ändern um Auswärtskoppeltermine zu ermöglichen", `TimePickerSecondTimeAuswaertskoppel` „Alternative Startzeit".

### Zustandslogik `EnableButtons`
- Detailpanel sichtbar ⇔ Modus = 1.
- Zweite Startzeit aktiv ⇔ Koppel = 1 (Koppelspieltag).
- Priorität aktiv ⇔ Koppel ≠ 0.
- Koppel = 1 ⇒ Auswärtskoppel-Checkbox wird **abgehakt und deaktiviert** (Koppelspieltag und Auswärtskoppel-Alternativzeit schließen sich aus, da beide `KoppelSecondTime` nutzen).
- Alternativzeit aktiv ⇔ Auswärtskoppel-Checkbox gesetzt.

## Fachliche Logik / Regeln
Mapping Form → `THomeDay` (`ButtonOKClick`):
- Modus 0: `NoDate=true, SperrTermin=false`; Modus 2: `NoDate=false, SperrTermin=true`. Übrige Felder bleiben unverändert aus der Eingangskopie.
- Modus 1: `Date := TimePicker.Time` (**nur Uhrzeitanteil**; das Datum kommt vom Aufrufer/Grid), `MaxParallelGame := ItemIndex` (0 = unbegrenzt), `AusweichTermin`, `Location`; `KoppelDate/KoppelAuswaertsSecondTime/DoubleDate := false`, dann:
  - Koppel 1 → `KoppelDate := true`, `KoppelSecondTime := FixDateTime(Frac(TimePickerKoppelTime))`.
  - Koppel 2 → `DoubleDate := true`.
  - Koppel ≠ 0 → `KoppelPrio := ComboBoxPrio.ItemIndex` (0 möglich, 1 normal, 2 hoch).
  - Koppel ≠ 1 und Checkbox → `KoppelAuswaertsSecondTime := true`, `KoppelSecondTime := Frac(Alternativzeit)`.
- Validierung `CheckValueData` (nur wenn `Value.IsHomeDay`):
  - Uhrzeit ≠ 00:00 („Bitte geben Sie eine Zeit ein").
  - Bei Koppelspieltag bzw. Auswärtskoppel-Alternativzeit: zweite Zeit ≠ 0 und `|HomeTime − SecondTime| > 3:59` → Meldung „Zwischen dem ersten und zweiten Spiel müssen mindestens 4 Stunden liegen". Schwellwert faktisch **> 3 h 59 min** (also ≥ 4 h bei Minutengenauigkeit).

## Daten & Persistenz
Keine direkte. `THomeDay` wird vom Aufrufer über `AsShortString`/`FromShortString` als Kurztext in die Kalender-Grid-Zelle serialisiert und später von `TPlanData.SetHomeDays` in `homegameday`-Knoten (Attribute `datetime`, `parallelgames`, `ausweichtermin`, `couplegameday`, `doublegameday`, `coupleprio`, `couplesecondtime`, `coupleauswaertssecondtime`, `location`) bzw. `nogameday` übertragen.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL, `TDateTimePicker` (Windows Common Control), `ShowMessage`.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI` → `EditHomeDayViewModel` + View; Modell `HomeDay` (Record/Klasse in `AndiGenerator.Core`) mit:
  - `enum HomeDayMode { NoHomeGame=0, HomeGame=1, Blocked=2 }` statt `NoDate`/`SperrTermin`-Bool-Paar,
  - `enum CoupleKind { None, SameDay, Weekend }` statt `KoppelDate`/`DoubleDate`,
  - `enum CouplePriority { Possible=0, Normal=1, High=2 }`,
  - `TimeOnly Time`, `TimeOnly? SecondTime`, `int MaxParallelGames` (0 = unbegrenzt), `bool IsFallback`, `string Location`.
- Enabled/Visible-Logik als berechnete ViewModel-Properties (`IsHomeGame`, `IsSecondTimeEnabled`, `IsPriorityEnabled`, `IsAwayCoupleEnabled`), Validierung per `INotifyDataErrorInfo`.
- Fallstricke:
  - `TDateTime`-Uhrzeitvergleich mit Double – in C# `TimeOnly`/`TimeSpan` exakt; die 3:59-Schwelle 1:1 als `> TimeSpan.FromMinutes(239)` übernehmen.
  - Bei Modus 0/2 bleiben alte Heimspiel-Felder in der Kopie erhalten – ob `AsShortString` diese ignoriert, beim Port prüfen (sonst „Geisterdaten").
  - `KoppelPrio` wird bei Koppel=0 nicht zurückgesetzt.
  - `GetValue` gibt internes Objekt zurück (Lebensdauer an Dialog gebunden) – in C# irrelevant.
- Aufwandsschätzung: **M** – viele abhängige Felder und Validierung, aber klar abgegrenzt.
- Auffälligkeiten: Uhrzeit 00:00 ist nicht als gültige Anstoßzeit erlaubt (fachlich ok); Mitternachts-Übertrag beim 4-h-Abstand (z.B. 22:00 und 01:00) wird nicht betrachtet; `TeamName`-Parameter ungenutzt; unbenutzte Unit `DialogPanelHomeDaysDetail` im uses (Zirkelbezug).

## Offene Fragen
- Soll bei Wechsel auf „kein Heimspiel"/„Sperrtermin" der Heimspiel-Detailzustand verworfen werden?
- Gilt die 4-Stunden-Regel auch für Doppelspieltage (aktuell nicht geprüft, da keine zweite Zeit)?
