# DialogEditOneGame (DialogEditOneGame.pas + DialogEditOneGame.dfm)

**Kategorie:** UI-Dialog
**Umfang:** 136 Zeilen .pas / 141 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `PlanTypes`, `PlanUtils` (`FixControls`, `FixDateTime`, `GetListComboValue`, `SetListComboValue`), `PlanDataObjects`; VCL: `Forms`, `StdCtrls`, `ComCtrls` (`TDateTimePicker`), `ExtCtrls`, `Dialogs`
**Verwendet von:** `DialogPanelPredefinedGames` (Bearbeiten Z. ~237, Neu Z. ~257)

## Zweck
Modaler Editor für **eine vordefinierte Begegnung** (fest vorgegebenes Spiel mit Datum/Uhrzeit, Heim-, Gastmannschaft und Spiellokal), die der Optimierer nicht mehr verschieben soll. Arbeitet auf einem `TDataObject`-Knoten `nGame` (`game`) unterhalb von `predefinedgames`.

## Inhalt / Struktur
- `TDialogEditOneGame = class(TForm)`, privat `PlanData: TPlanData` (nur gespeichert, sonst ungenutzt).
- `SetValue(PlanData, Value)`:
  - `ComboHomeTeam`/`ComboGuestTeam` ← alle Teamnamen (sortiert).
  - `ComboLocation` ← `getTeamLocationsInt` = `''`, `'1'..'5'` (click-TT kennt nur numerische Spiellokale).
  - `DatePicker` ← Saisonbeginn `root.aFrom` (Default für Neuanlage).
  - Bei `Value<>nil`: Datum/Uhrzeit aus `aDateTime` (Trunc = Datum, Rest = Zeit), Heim/Gast/Lokal vorselektieren.
- `GetValue(Value)`: `aDateTime` = `FixDateTime(Trunc(DatePicker) + Frac(TimePicker))`, `aHomeTeamName`, `aGuestTeamName`, `aLocation` (leer = Standardlokal).
- `ButtonOKClick`: Validierung (s.u.).
- `EnableButtons`: berechnet `HomeTeamName`, macht aber nichts damit (Rest-/Platzhaltercode).

UI (.dfm, Caption „Begegnung", 409×289): Datum (`DatePicker`), Uhrzeit (`TimePicker`, `dtkTime`, Default 20:00 aus 42525.8333 = 04.06.2016 20:00), Heimmannschaft, Gastmannschaft, Spiellokal (alle `csDropDownList`); OK/Abbrechen.

## Fachliche Logik / Regeln
- Heimmannschaft gewählt, Gastmannschaft gewählt, Heim ≠ Gast („Die Mannschaft kann nicht gegen sich selbst spielen").
- Keine Prüfung auf Saisonzeitraum, Datum, Doppelung oder Uhrzeit ≠ 0.
- `FixDateTime` kompensiert Double-Rundungsfehler bei TDateTime-Addition (Decode/Encode).

## Daten & Persistenz
Keine direkte; Attribute `datetime`, `hometeamname`, `guestteamname`, `location` am Knoten `game`.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL `TDateTimePicker` (Windows Common Control), `ShowMessage`.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI` → `EditGameViewModel` (Properties `DateOnly Date`, `TimeOnly Time`, `string? HomeTeam`, `string? GuestTeam`, `string Location`), Modell `PredefinedGame` Record mit `DateTime DateTime`.
- Kombination Datum+Zeit in C# exakt über `date.ToDateTime(time)` – `FixDateTime` entfällt.
- Picker: Avalonia `DatePicker`/`TimePicker` bzw. `CalendarDatePicker`.
- Aufwandsschätzung: **S**.
- Auffälligkeiten: `EnableButtons` wirkungslos (Z. 88–97); `PlanData`-Feld ungenutzt; Spiellokal-Liste fest 1–5 (hängt an `TPlanData.getTeamLocationsInt`).

## Offene Fragen
- Soll geprüft werden, dass das Datum innerhalb der Saison liegt bzw. die Paarung existiert/nicht doppelt ist?
