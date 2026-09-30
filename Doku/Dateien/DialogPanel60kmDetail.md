# DialogPanel60kmDetail (DialogPanel60kmDetail.pas + DialogPanel60kmDetail.dfm)

**Kategorie:** UI-Panel (eingebettet) – Detail-Panel je Mannschaft
**Umfang:** 234 Zeilen .pas / 59 Zeilen .dfm
**Abhängigkeiten (uses):** DialogTeamPanel (Basisklasse `TDialogTeamPanel`), DialogPanel, PlanDataObjects (`TPlanData`, `TDataObject`, Konstanten `nTeam`, `nNoWeekGames`, `aTeamName`), PlanTypes, PlanUtils (`FixControls`); ungenutzt: DialogPanelOptionArray, DialogForSingleGewichtungsOptions; VCL (ComCtrls `TListView`)
**Verwendet von:** DialogPanel60km (`getDialogClass`)

## Zweck
Bearbeitet für **eine** Mannschaft (`TeamName`) die Liste der Gegner, gegen die wegen der **60-km-Regel** (lange Anfahrt) nur am Wochenende gespielt werden darf. Darstellung als Checkbox-Liste aller anderen Mannschaften der Liga.

## Inhalt / Struktur
- `TDialogPanel60kmDetail = class(TDialogTeamPanel)`
  - Controls: `PanelMain` → `PanelBottom` (BorderWidth 16) → `ListViewTeams` (`vsReport`, `Checkboxes = True`, eine Spalte „Mannschaft“, `OnClick = ListViewTeamsClick`).
  - `DefaultValues: TStrings` – angelegt/freigegeben, **nie benutzt**.
- Methoden:
  - `TeamListFromData(List, PlanData)` – liest aus dem Team-Knoten (`team[teamname=TeamName]`) alle Kinder `noweekgames` → `aTeamName`; entfernt Namen, die keiner existierenden Mannschaft entsprechen.
  - `TeamListToForm(List)` – alle Mannschaften (`getTeamNames`, sortiert) außer der eigenen als Zeilen; angehakt, wenn in der Liste.
  - `FormToTeamList(List)` – alle angehakten Zeilen.
  - `DataToForm` = `TeamListFromData(PlanData)` + `TeamListToForm`.
  - `FormToData` – im Team-Knoten `deleteChilds(nNoWeekGames)`, dann je angehaktem Gegner neuer Knoten `noweekgames teamname="…"`.
  - `isDefault` – Default-Liste aus `PlanDataDefault` (leer, wenn nil), **ruft `FormToData()` auf** (Seiteneffekt), vergleicht Mengen (gleiche Anzahl + jede Default-Mannschaft enthalten).
  - `toDefault` – Default-Liste ins Formular (schreibt **nicht** sofort in PlanData; das geschieht beim nächsten `isDefault`/`FormToData`).
  - `ListViewTeamsClick` → `FireOnChange`.
  - `setPanel` – `PanelMain.Parent := Host`.

## Fachliche Logik / Regeln
- 60-km-Regel: Spiele zwischen `TeamName` und den angehakten Gegnern nur am Wochenende. Ob Freitag als Wochenende gilt, steuert `TCalculateOptions.FreitagIsAllowedBy60km` (DialogOptions). Kostenart `mkt60Kilometer` („60km Regel“).
- Gespeichert wird die Beziehung **einseitig** beim bearbeiteten Team; die Engine lädt sie in `TMannschaft.loadNoWeekGame` in die Liste `noWeekGame` dieser Mannschaft.

## Daten & Persistenz
DataObject-Baum: `plan/team[@teamname]/noweekgames[@teamname]` (Schlüssel `teamname`). Persistenz indirekt über `*.modifications`-Diff (siehe DialogPanel.md).

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL `TListView` mit Checkboxen (Win32 ListView); Checkbox-Änderungen werden nur über `OnClick` erkannt – Umschalten per Leertaste löst ggf. kein `OnChange` des Panels aus.

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels.NoWeekGamesTeamViewModel : TeamEditorPageViewModel` + View mit `ItemsControl`/`ListBox` aus `CheckableItem { string Team; bool IsChecked }`.
- Datenmodell: im Core statt DataObject-Baum besser `Team.NoWeekdayOpponents : HashSet<string>` (bzw. Team-IDs); Mapper zum DataObject-/XML-Format für Kompatibilität.
- `isDefault` als reine Mengen-Gleichheit ohne Seiteneffekt (`SetEquals`).
- Änderungserkennung über `PropertyChanged` der Items (deckt Maus und Tastatur ab).
- **Aufwand:** S.
- **Auffälligkeiten:**
  - `DefaultValues` unbenutzt (Z. 29, 126, 132).
  - `isDefault` schreibt via `FormToData` in die Arbeitskopie (Z. 190).
  - `PanelMain` hat in der .dfm `Align = alTop` mit fester Höhe 265 px (nicht `alClient`) – Liste wird im Host nicht vergrößert.
  - Kommentar `{ TDialogPanelFreeDays }` (Z. 48) ist Copy-&-Paste-Rest.

## Offene Fragen
- Soll die 60-km-Beziehung symmetrisch sein (A↔B automatisch)? Aktuell muss sie ggf. bei beiden Mannschaften gepflegt werden bzw. die Engine entscheidet – in PlanTypes prüfen.
