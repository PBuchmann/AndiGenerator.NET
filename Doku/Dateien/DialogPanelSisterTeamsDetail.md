# DialogPanelSisterTeamsDetail (DialogPanelSisterTeamsDetail.pas + DialogPanelSisterTeamsDetail.dfm)

**Kategorie:** UI-Panel (eingebettet) – Detail-Panel pro Mannschaft
**Umfang:** 306 Zeilen .pas / 99 Zeilen .dfm
**Abhängigkeiten (uses):** DialogTeamPanel, DialogPanel, PlanDataObjects (`TDataObject`, `TDataObjectKey`, `TPlanData`), DialogEditSisterTeam (`TDialogEditSisterTeam`), PlanUtils (`FixControls`), System.UITypes; VCL: TListBox, TButton, `MessageDlg`
**Verwendet von:** DialogPanelSisterTeams (über `getDialogClass`)

## Zweck
Zeigt und bearbeitet für **eine Mannschaft** (`TeamName`) die Liste ihrer **Nachbarmannschaften** (Mannschaften desselben Vereins in anderen Ligen) inkl. deren Spieltermine. Neu/Bearbeiten erfolgt über den modalen Dialog `TDialogEditSisterTeam`; das Panel arbeitet **direkt auf dem `TPlanData`-Baum** (kein Puffer, `FormToData` ist leer).

## Inhalt / Struktur
- `TDialogPanelSisterTeamsDetail = class(TDialogTeamPanel)`
- Methoden:
  - `getMainNode(PlanData)` → sucht unter `root` den Knoten `team` mit Attribut `teamname = TeamName` über `TDataObjectKey` + `findChildIndexByKey` (indexbasierte Schlüsselsuche).
  - `SisterTeamNodeToString(Node)` → Anzeigetext: `"(<gender>) <teamname> <n>, Spiele"` + optional `", Spiellokal: <location>"`, `", gleichzeitige Spiele vermeiden"` (Attribut `noparallelgames`), `", gleichzeitige Heimspiele erwünscht"` (`parallelhomegames`). `n` = Anzahl `sistergame`-Kinder.
  - `DataToForm` → alle `sisterteam`-Kinder des Team-Knotens als Strings, **sortiert**, in `ListBoxTeams`; alte Selektion per Index wiederhergestellt.
  - `getSelectedNode` → findet den Knoten, dessen **Anzeigetext** dem selektierten Listeneintrag gleicht (letzter Treffer gewinnt).
  - `ButtonTeamNewClick` → `TDialogEditSisterTeam.SetValues(nil, PlanData, TeamName)`; bei OK neuer `sisterteam`-Knoten unter dem Team-Knoten, `Dialog.GetValues(Node)`.
  - `ButtonTeamEditClick` / `ListBoxTeamsDblClick` → Dialog mit selektiertem Knoten.
  - `ButtonTeamDeleteClick` → Rückfrage „Möchten Sie die Mannschaft … wirklich löschen?" → `parent.RemoveChild`.
  - `isDefault` → `TDataObject.ChildNodesAreTheSame(Node1, Node2, nSisterTeam)` (aktuelle vs. click-TT-Default).
  - `toDefault` → entfernt alle `sisterteam`-Kinder, kopiert die des Default-Team-Knotens per `assign`.
  - `EnableButtons` → Löschen/Bearbeiten nur mit gültiger Selektion.
- UI (.dfm): Überschrift „Nachbarmannschaften"; `ListBoxTeams` (alClient); rechts Buttons „Neue Mannschaft…", „Mannschaft bearbeiten…", „Mannschaft Löschen…".

## Fachliche Logik / Regeln
- Datenmodell: `<team teamname="X"><sisterteam gender=… teamname=… location=… noparallelgames=… parallelhomegames=…><sistergame …/>…</sisterteam></team>`.
- Fachliche Bedeutung (Kostenfunktion, nicht hier): Nachbarmannschaftsspiele → `mktSisterGames`, Hallenbelegung; Flags „gleichzeitige Spiele vermeiden" / „gleichzeitige Heimspiele erwünscht".

## Daten & Persistenz
Nur In-Memory `TPlanData`; Persistenz (XML) erfolgt woanders. Attribute/Tags: `team`, `teamname`, `sisterteam`, `sistergame`, `gender`, `location`, `noparallelgames`, `parallelhomegames`.

## Threading / Performance
Keine. `getSelectedNode` baut für jeden Eintrag den Anzeigestring neu (O(n)) – unkritisch.

## Plattformabhängigkeiten
VCL-ListBox, `MessageDlg`, modale Formulare (`ShowModal`).

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Panels.SisterTeamsDetailViewModel : TeamDetailPanelViewModel`, `ObservableCollection<SisterTeamItem>` mit **Referenz auf das Datenobjekt** statt Stringvergleich.
- Anzeige-Text als `ToString()`/Converter; Sortierung wie Original (ordinal/kulturabhängig? Delphi `TStringList.Sort` ist **case-insensitiv, locale-basiert** – `StringComparer.CurrentCultureIgnoreCase`).
- Aufwand: **S–M**.
- **Bugs/Auffälligkeiten:**
  - Z. 75: Textformat `"… 3, Spiele"` – Komma an falscher Stelle (vermutlich `", 3 Spiele"` gemeint).
  - Z. 238: Identifikation per Anzeigetext → zwei Nachbarmannschaften mit identischem Text sind nicht unterscheidbar (immer die letzte wird bearbeitet/gelöscht).
  - Z. 180: `getMainNode` kann `nil` sein (Team existiert nicht) → `TDataObject.Create(nSisterTeam, nil)` erzeugt einen verwaisten Knoten (Speicherleck, Daten verloren).
  - Z. 282: `toDefault` greift ohne nil-Prüfung auf `Node1` zu → Zugriffsverletzung, wenn Team nur im Default existiert (z. B. umbenannt).
  - Mannschafts-Umbenennung (DialogPanelTeams) ändert `teamname` – Default-Vergleich über `getMainNode(PlanDataDefault)` findet das Team dann nicht mehr (Node2 = nil).

## Offene Fragen
- Gewünschtes Anzeigeformat der Liste?
- Soll der Default-Vergleich den ursprünglichen Namen (`orgteamname`) berücksichtigen?
