# DialogEditTeamName (DialogEditTeamName.pas + DialogEditTeamName.dfm)

**Kategorie:** UI-Dialog
**Umfang:** 105 Zeilen .pas / 149 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `PlanDataObjects` (`TPlanData.getTeamLocationsInt`), `PlanUtils` (`FixControls`, `Get/SetListComboValue`), `PlanTypes`; VCL: `Forms`, `StdCtrls`, `ExtCtrls`, `Samples.Spin`, `Dialogs`
**Verwendet von:** tatsächlich instanziiert nur in `DialogPanelTeams` (Bearbeiten Z. ~133, Neu Z. ~222). Zusätzlich im uses (ungenutzt) von: `DialogEditSisterTeam`, `DialogPanel60km`, `DialogPanelAuswaertskoppel`, `DialogPanelHomeCoupleCompact`, `DialogPanelHomeDays`, `DialogPanelHomeRight`, `DialogPanelLocationsCompact`, `DialogPanelPredefinedGames`, `DialogPanelSisterTeams`.

## Zweck
Modaler Editor der **Stammdaten einer Mannschaft** der geplanten Liga: Name, click-TT-Mannschafts-ID, Vereins-ID, Mannschaftsnummer und Standard-Spiellokal. Dient zum Anlegen neuer bzw. Korrigieren importierter Mannschaften.

## Inhalt / Struktur
- `TFormEditTeamName = class(TForm)`.
- `SetValues(PlanData, TeamName, TeamNumber, TeamId, ClubId, Location)` – befüllt Felder; Lokal-Combo aus `getTeamLocationsInt` ('' + '1'..'5').
- `GetValues(var TeamName, var TeamNumber, var TeamId, var ClubId, var Location)` – liefert getrimmte Werte (var-Parameter).
- `ButtonOKClick` – Validierung; `ButtonCancelClick` → `mrCancel`; `FormCreate` → `FixControls`.

UI (.dfm, Caption „Mannschaft", 615×230): Mannschaftsname (`EditName`), Mannschafts-ID (`EditId`), Vereins-ID (`EditClubId`), Mannschaftsnummer (`TSpinEdit` 1..100, Hinweis „1., 2., 3. Mannschaft"), Spiellokal (`csDropDownList`, Hinweis „leer, falls Verein nur in einem Spiellokal spielt"); OK/Abbrechen.

## Fachliche Logik / Regeln
- Pflichtfelder (getrimmt nicht leer): Mannschaftsname, Mannschafts-ID, Vereins-ID.
- Keine Eindeutigkeitsprüfung von Name/ID hier (ggf. im aufrufenden Panel).
- Vereins-ID ist fachlich relevant für Vereinszugehörigkeit (z.B. vereinsinterne Spiele, Hallenbelegung pro Verein) – Auswertung in anderen Units.

## Daten & Persistenz
Keine direkte; der Aufrufer schreibt die Werte in den `team`-Knoten (`teamname`, `teamid`, `clubid`, `teamnumber`, `location`).

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL, `TSpinEdit`, `ShowMessage`.

## Migrationshinweise für C#
- Ziel: `EditTeamViewModel` (Properties `Name`, `TeamId`, `ClubId`, `int Number` [1..100], `string Location`), Rückgabe als Record `TeamMasterData`. `var`-Parameter → Rückgabeobjekt.
- `TSpinEdit` → `NumericUpDown` (Avalonia).
- Validierung per `INotifyDataErrorInfo`; ggf. Eindeutigkeit von Name/ID ergänzen.
- Aufwandsschätzung: **S**.
- Auffälligkeiten: Unit wird in ~9 Units unnötig importiert (reine uses-Leichen).

## Offene Fragen
- Soll die Eindeutigkeit von Mannschaftsname/-ID beim Anlegen geprüft werden?
