# DialogPanelPredefinedGames (DialogPanelPredefinedGames.pas + DialogPanelPredefinedGames.dfm)

**Kategorie:** UI-Panel (eingebettet)
**Umfang:** 349 Zeilen .pas / 108 Zeilen .dfm
**Abhängigkeiten (uses):** DialogPanel, PlanDataObjects (`TDataObject`, `TPlanData.FixLocation`, `NodesAreTheSame`), DialogEditOneGame (`TDialogEditOneGame`), PlanUtils (`DateToString`, `TimeToString`, `DateFromString`, `TimeFromString`, `FixDateTime`, `FixControls`), Math (`max`), System.UITypes; VCL: TStringGrid, `MessageDlg`
**Verwendet von:** AndiGeneratorMain (Daten-Dialog)

## Zweck
Seite „**Manuell festgelegte Begegnungen**": Liste von Spielen, deren Termin (Datum, Uhrzeit, Heim, Gast, Spiellokal) vom Benutzer **fest vorgegeben** wird und vom Optimierer nicht verändert werden soll. Anlegen/Bearbeiten über `TDialogEditOneGame`, Löschen mit Rückfrage. Arbeitet direkt auf dem `TPlanData`-Baum (`FormToData` leer).

## Inhalt / Struktur
- `DataToForm` → Grid-Kopf „Datum | Uhrzeit | Heimmannschaft | Gastmannschaft | Spiellokal" (Spaltenbreiten 100/80/200/200/200); für jedes `game` unter `predefinedgames`: Datum (`DateToString(Trunc)`), Zeit (`TimeToString(FixDateTime(frac))`), `hometeamname`, `guestteamname`, `PlanData.FixLocation(location)`; danach `SortGrid`; alte Zeile wiederherstellen; `EnableButtons`.
- `SortGrid` → sortiert Grid-Zeilen nach Datum+Uhrzeit (aus den **Grid-Strings zurückgeparst**) per Bubble-Sort mit Neustart bei jedem Tausch (O(n³) im Worst Case).
- `getMainNode` / `getOrCreateMainNode` → `predefinedgames`-Knoten.
- `getSelectedGameNode` → findet den `game`-Knoten, dessen formatiertes Datum/Zeit/Heim/Gast der selektierten Grid-Zeile gleicht (letzter Treffer).
- `ButtonGameNewClick` → Dialog, bei OK neuer `game`-Knoten (Container ggf. angelegt), `Dialog.GetValue(Node)`.
- `ButtonGameEditClick` / `GridDblClick` → Dialog mit vorhandenem Knoten.
- `ButtonGameDeleteClick` → „Möchten Sie das Spiel wirklich löschen?" → `RemoveChild`.
- `isDefault` → `TDataObject.NodesAreTheSame(predefinedgames aktuell, Default)`.
- `toDefault` → Knoten entfernen bzw. aus Default per `assign` kopieren.
- UI (.dfm): Überschrift, `Grid` (TStringGrid, RowSelect, FixedCols=0), Buttons „Neue Begenung…" (Tippfehler), „Begegnung bearbeiten…", „Begegnung Löschen…".

## Fachliche Logik / Regeln
- Vorgegebene Begegnungen: Heim-/Gastmannschaft + Datum/Uhrzeit + Spiellokal. Wirkung im Optimierer (Fixierung) liegt in PlanTypes/PlanData (nicht hier).
- `FixLocation` normalisiert den Spiellokal-Wert (Default-Spiellokal-Auflösung in PlanDataObjects).

## Daten & Persistenz
`<predefinedgames><game datetime=… hometeamname=… guestteamname=… location=…/>…</predefinedgames>` unter `root`.

## Threading / Performance
Keine. `SortGrid` ist ineffizient, aber n (Spiele) ist klein.

## Plattformabhängigkeiten
`TStringGrid`, `MessageDlg`, modales Formular; Datums-/Zeit-Formatierung über PlanUtils (locale-abhängig).

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Panels.PredefinedGamesPanelViewModel` mit `ObservableCollection<PredefinedGameVm>` (Referenz auf Datenknoten) + `DataGrid`, sortiert per LINQ `OrderBy(g => g.DateTime)`.
- `TDateTime` → `DateTime` (Datum + Uhrzeit, ohne Zeitzone). `FixDateTime` (Rundung von Double-Zeitanteilen) entfällt bei `DateTime`, beim Import aber Rundung auf Minuten beachten.
- Kein Zurückparsen formatierter Strings (Locale-Falle).
- Aufwand: **S–M**.
- **Bugs/Auffälligkeiten:**
  - Z. 63–91: Sortierung über Rück-Parsing der Anzeige-Strings; leere Zeile (keine Spiele) → `DateFromString('')` – Verhalten abhängig von PlanUtils.
  - Z. 193–203: Identifikation per formatiertem Text → identische Begegnungen (gleiches Datum, Zeit, Teams) nicht unterscheidbar.
  - Z. 219: Löschen ohne Prüfung, ob `MainNode` existiert (ist gesichert, da `GameNode` nur bei vorhandenem MainNode gefunden wird).
  - Keine Validierung (z. B. Heim = Gast, Termin außerhalb Saison) – evtl. in `TDialogEditOneGame`.
  - Mannschafts-Umbenennung (DialogPanelTeams) aktualisiert `hometeamname`/`guestteamname` hier **nicht** → verwaiste Referenzen möglich.

## Offene Fragen
- Wie verhalten sich vorgegebene Spiele bei Mannschafts-Umbenennung (siehe DialogPanelTeams)?
