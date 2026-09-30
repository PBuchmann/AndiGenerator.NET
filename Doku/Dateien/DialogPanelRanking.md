# DialogPanelRanking (DialogPanelRanking.pas + DialogPanelRanking.dfm)

**Kategorie:** UI-Panel (eingebettet)
**Umfang:** 252 Zeilen .pas / 95 Zeilen .dfm
**Abhängigkeiten (uses):** DialogPanel, PlanDataObjects (`TPlanData.getTeamNames`, `TDataObject`), PlanUtils (`FixControls`), System.Types (`Point`); VCL: TListBox (Drag&Drop), TCheckBox, TButton
**Verwendet von:** AndiGeneratorMain (Daten-Dialog), DialogFirstStart (Einrichtungs-Assistent)

## Zweck
Seite „**Setzliste**": Der Benutzer gibt die **erwartete Abschlusstabelle** vor (Reihenfolge der Mannschaften). Ziel laut UI-Text: Am Saisonende sollen die stärksten und die schwächsten Mannschaften gegeneinander spielen, damit in Auf-/Abstiegsspielen keine Mannschaften beteiligt sind, „bei denen es um nichts mehr geht"; Spannung bis zum Schluss, „Mauscheleien" verhindern. Aktivierbar per Checkbox.

## Inhalt / Struktur
- `DataToForm` →
  1. `ranking`-Knoten suchen; `CheckBoxActive.Checked := active`.
  2. Für jedes `team`-Kind mit gültigem `teamname` (existiert in `getTeamNames`): `while Items.Count <= rankingindex do Items.Add(TeamName)` (siehe Bug).
  3. Leere Einträge entfernen.
  4. Alle Mannschaften, die noch nicht in der Liste sind, hinten anhängen (alphabetisch, aus `getTeamNames`).
  5. Erste Zeile selektieren, `EnableButtons`.
- `FormToData` → `getOrCreateChild('ranking')`, `clear`, `active` setzen, für jede Listenposition `team teamname=… rankingindex=i` (0-basiert).
- `ButtonUpClick` / `ButtonDownClick` → `Items.Exchange` mit Nachbar.
- `ListRankingDragDrop` / `DragOver` → Umsortieren per Drag&Drop innerhalb der Liste (`ItemAtPos`, Delete+Insert).
- `isDefault` → `not CheckBoxActive.Checked`; `toDefault` → Checkbox aus (Reihenfolge bleibt).
- UI (.dfm): Erklärtext (s. Zweck), `CheckBoxActive` „Setzliste aktivieren", Label „Erwartete Reihenfolge:", `ListRanking` (DragMode=dmAutomatic), Buttons „Nach oben", „Nach unten".

## Fachliche Logik / Regeln
- Setzliste = Reihenfolge 0..n−1 (0 = erwarteter Tabellenerster). Die eigentliche Kostenregel (`mktRanking`: Spitzen- und Kellerduelle am Saisonende) ist in PlanTypes implementiert, nicht hier.
- Default: Setzliste inaktiv.

## Daten & Persistenz
`<ranking active=…><team teamname=… rankingindex=…/>…</ranking>` unter `root`.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL-Drag&Drop (`DragMode`, `OnDragOver/OnDragDrop`, `ItemAtPos`), TListBox.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Panels.RankingPanelViewModel` mit `ObservableCollection<string>` + `MoveUp/MoveDown`-Commands; Drag&Drop in Avalonia über `DragDrop`-API oder Behavior (optional, Buttons genügen funktional).
- Laden robust: nach `rankingindex` sortieren (`OrderBy`) statt Auffüllschleife.
- Aufwand: **S**.
- **Bugs/Auffälligkeiten:**
  - Z. 110–113: `while ListRanking.Items.Count <= RankingIndex do Items.Add(TeamName)` füllt Lücken mit **demselben Teamnamen** statt mit Platzhaltern und setzt nicht an Position `RankingIndex`. Funktioniert nur, wenn die Knoten in aufsteigender, lückenloser Indexreihenfolge vorliegen (wie `FormToData` sie schreibt). Bei anderer Reihenfolge/Lücken (z. B. nach Löschen einer Mannschaft aus der Liga → Lücke) entstehen **Duplikate** bzw. falsche Positionen; die Entfernung leerer Einträge (Z. 117–123) greift nie.
  - Up/Down/Drag&Drop rufen **kein `FireOnChange`** – nur die Checkbox tut das (für den Standard-Button egal, da `isDefault` nur die Checkbox prüft).
  - Up/Down: `Items.Exchange` verschiebt die Selektion nicht explizit mit; ob `ItemIndex` beim VCL-ListBox-Exchange mitwandert, ist Implementierungsdetail → am Original prüfen (UX).
  - Z. 213: Drop unterhalb des letzten Eintrags → `ItemAtPos(..., True)` = −1 → `Items.Insert(-1, S)` → Exception (Listenindex außerhalb).
  - `toDefault` stellt die Reihenfolge nicht zurück.

## Offene Fragen
- Soll „Standard" auch die Reihenfolge zurücksetzen?
