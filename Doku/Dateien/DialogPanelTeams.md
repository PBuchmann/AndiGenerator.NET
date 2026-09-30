# DialogPanelTeams (DialogPanelTeams.pas + DialogPanelTeams.dfm)

**Kategorie:** UI-Panel (eingebettet)
**Umfang:** 451 Zeilen .pas / 85 Zeilen .dfm
**Abhängigkeiten (uses):** DialogPanel, PlanDataObjects (`TPlanData.getTeamNames`, `FixLocation`, `TDataObject.assign/assignAttributes`), DialogEditTeamName (`TFormEditTeamName`), PlanUtils (`FixControls`), System.UITypes; VCL: TListBox, `MessageDlg`, `ShowMessage`
**Verwendet von:** AndiGeneratorMain (Daten-Dialog)

## Zweck
Seite „**Mannschaften**": Verwaltung der Mannschaften der Liga (Anlegen, Bearbeiten/Umbenennen, Löschen) mit Eindeutigkeitsprüfungen. Arbeitet direkt auf den `team`-Knoten in `TPlanData.root` (`FormToData` leer). Enthält eine Rücksetz-Logik auf die click-TT-Importdaten, die Umbenennungen über das Attribut `orgteamname` nachverfolgt.

## Inhalt / Struktur
- `DataToForm` → `PlanData.getTeamNames(ListBoxTeams.Items)` (alphabetisch sortiert), alte Selektion per Index.
- `ButtonTeamNewClick` → `TFormEditTeamName` modal in Schleife bis OK+gültig oder Abbruch; Werte: `TeamName`, `TeamNumber`, `TeamId`, `ClubId`, `DefaultLocation`; Eindeutigkeitsprüfung (s. u.); neuer `team`-Knoten mit `teamname`, `teamid`, `clubid`, `location`, `teamnumber`.
- `ButtonTeamEditClick` / Doppelklick → Originalknoten suchen, Werte in Dialog (`location` via `FixLocation`), Schleife wie oben (eigener Knoten von Prüfung ausgenommen). Bei Namensänderung Pflege von `orgteamname`:
  - neuer Name = `orgteamname` → `orgteamname` leeren (zurückbenannt),
  - `orgteamname` schon gesetzt → unverändert lassen,
  - sonst `orgteamname := alter teamname`.
- `ButtonTeamDeleteClick` → Rückfrage, ersten Knoten mit passendem `teamname` entfernen.
- `isDefault` → beidseitiger Mengenvergleich der Teams (Name, TeamId, Location, ClubID, TeamNumber) zwischen PlanData und PlanDataDefault.
- `toDefault` → Abgleich mit Default über `IsTheSameTeam` (Match über `orgteamname`, falls gesetzt, sonst `teamname`):
  1. im Default vorhandene, lokal fehlende Teams wieder anlegen (Kopie per `assign`, inkl. Unterknoten),
  2. lokal hinzugefügte Teams löschen,
  3. bei gematchten Teams **nur Attribute** zurückkopieren (`assignAttributes`) – Unterknoten (Heimspieltage, Nachbarmannschaften …) bleiben.
- UI (.dfm): Label „Mannschaften", `ListBoxTeams`, Buttons „Neue Mannschaft…", „Mannschaft bearbeiten…", „Mannschaft Löschen…".

## Fachliche Logik / Regeln
Eindeutigkeit einer Mannschaft (Meldungen per `ShowMessage`, Dialog öffnet erneut):
1. Mannschaftsname eindeutig („Mannschaftsname ist nicht eindeutig").
2. Mannschafts-ID eindeutig („Mannschafts-ID ist nicht eindeutig") – **fehlerhaft implementiert**, s. Bugs.
3. Kombination (Verein `clubid`, Mannschaftsnummer `teamnumber`) eindeutig („Zu diesem Verein gibt es schon eine Mannschaft mit dieser Nummer").

## Daten & Persistenz
`<team teamname=… teamid=… clubid=… teamnumber=… location=… orgteamname=…>…</team>` unter `root`.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL-ListBox, `MessageDlg`/`ShowMessage`, modale Dialogschleife.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Panels.TeamsPanelViewModel`; Eindeutigkeitsprüfung als reine Funktion in `AndiGenerator.Core` (`TeamValidator.Validate(team, allTeams, exclude)`), gut unit-testbar.
- `toDefault`-Abgleich ebenfalls in Core (`TeamMerge.ResetToDefault(planData, defaultData)`).
- Dialog-Schleife → Dialog-ViewModel mit Validierung vor Schließen.
- Aufwand: **M** – Rücksetz-/Umbenennungslogik mit Randfällen.
- **Bugs/Auffälligkeiten:**
  - Z. 159 und Z. 244: `if TeamName = TeamNode.GetAsString(aTeamId)` – vergleicht den **Namen** mit der ID der anderen Mannschaft; gemeint war sicher `TeamId = …`. Die ID-Eindeutigkeit wird faktisch nie geprüft.
  - Z. 118–127: Wird der Originalknoten nicht gefunden, ist die for-in-Laufvariable `OrgTeamNode` nach der Schleife undefiniert (Delphi-Semantik) → potenzielle Zugriffsverletzung (praktisch unwahrscheinlich, da Liste aus denselben Daten).
  - Umbenennung propagiert den Namen **nicht** in andere Referenzen (`ranking/team@teamname`, `predefinedgames/game@hometeamname/guestteamname`, globale Variable `LastTeam` in DialogPanelMultiTeams). Detail-Panels mit `getMainNode(PlanDataDefault)` (z. B. SisterTeamsDetail) finden das Team im Default nicht mehr.
  - `isDefault` vergleicht nur Attribute, nicht `orgteamname` → nach Umbenennen und Zurückbenennen korrekt „default".
  - Anlegen mit leerem Namen wird hier nicht verhindert (evtl. in `TFormEditTeamName`).

## Offene Fragen
- Soll eine Umbenennung Referenzen (Setzliste, vorgegebene Spiele) automatisch mitziehen?
- Ist die ID-Prüfung gewollt (Bug beheben) – könnte bestehende Daten mit doppelten IDs blockieren?
