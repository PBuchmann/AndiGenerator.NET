# DialogEditOneSisterGame (DialogEditOneSisterGame.pas + DialogEditOneSisterGame.dfm)

**Kategorie:** UI-Dialog
**Umfang:** 138 Zeilen .pas / 152 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `PlanTypes`, `PlanUtils` (`FixControls`, `FixDateTime`, `GetListComboValue`, `SetListComboValue`), `PlanDataObjects`; VCL: `Forms`, `StdCtrls`, `ComCtrls` (`TDateTimePicker`), `ExtCtrls`, `Dialogs`
**Verwendet von:** `DialogEditSisterTeam` (Neu Z. 322, Bearbeiten Z. 303)

## Zweck
Modaler Editor für **ein Spiel einer Nachbarmannschaft** (Sister Team = andere Mannschaft desselben Vereins in einer anderen Liga, deren feststehende Spiele die Hallenbelegung beeinflussen). Der Gegner ist freier Text, da er nicht zur geplanten Liga gehört. Arbeitet auf `TDataObject`-Knoten `nSisterGame` (`sistergame`).

## Inhalt / Struktur
- `TDialogEditOneSisterGame = class(TForm)`, privat `PlanData`, `SisterTeamName`.
- `SetValue(PlanData, Value, SisterTeamName, MainTeamName)`:
  - `DatePicker` ← Saisonbeginn `root.aFrom`; `ComboLocation` ← '' + '1'..'5'.
  - Bei `Value`: Datum/Zeit aus `aDateTime`; ist `aHomeTeamName = SisterTeamName` → Art „Heimspiel", Gegner = Gastname, sonst „Auswärtsspiel", Gegner = Heimname; Lokal vorselektieren.
  - `MainTeamName` ungenutzt.
- `GetValue(Value)`: `aDateTime = FixDateTime(Trunc(Datum)+Frac(Zeit))`; Heimspiel → home=Sister, guest=Gegner; Auswärts → umgekehrt; `aLocation`.
- `ButtonOKClick`: Gegner darf nicht leer sein.
- `EnableButtons` leer.

UI (.dfm, Caption „Begegnung", 608×289): Datum, Uhrzeit (Default 20:00), Art (`ComboGameType`: 0 „Heimspiel", 1 „Auswärtsspiel"), Gegner (`EditGegner`, Freitext), Spiellokal (Hinweis „leer: Standardspiellokal der Mannschaft"); OK/Abbrechen.

## Fachliche Logik / Regeln
- Heim/Auswärts wird aus der Rolle des Sister-Teams in `hometeamname`/`guestteamname` abgeleitet.
- Der Gegnername wird **nicht** getrimmt gespeichert (`EditGegner.Text`), nur für die Leerprüfung getrimmt.
- Keine Prüfung Saisonzeitraum/Uhrzeit.

## Daten & Persistenz
Keine direkte; Attribute `datetime`, `hometeamname`, `guestteamname`, `location` am Knoten `sistergame`.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL, `TDateTimePicker`, `ShowMessage`.

## Migrationshinweise für C#
- Ziel: `EditSisterGameViewModel` (`DateOnly Date`, `TimeOnly Time`, `bool IsHome`, `string Opponent`, `string Location`) → Modell `SisterGame(DateTime, string Home, string Guest, string Location)`.
- Gemeinsame Basis mit `DialogEditOneGame` möglich (`GameEditorViewModelBase` mit Datum/Zeit/Lokal).
- Fallstrick: Umbenennung des Sister-Teams wird im übergeordneten Dialog über `OldSisterTeamName` nachgezogen – der hier übergebene Name ist der *aktuelle* Editname.
- Aufwandsschätzung: **S**.
- Auffälligkeiten: `MainTeamName`, `PlanData`, `EnableButtons` ungenutzt; Gegner ungetrimmt (Z. 96/101).

## Offene Fragen
- Soll der Gegnername getrimmt werden (Konsistenz mit anderen Namen)?
