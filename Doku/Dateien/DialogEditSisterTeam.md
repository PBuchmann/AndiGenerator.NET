# DialogEditSisterTeam (DialogEditSisterTeam.pas + DialogEditSisterTeam.dfm)

**Kategorie:** UI-Dialog
**Umfang:** 421 Zeilen .pas / 249 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen genutzt: `PlanDataObjects` (`TDataObject`, `TPlanData`, `DateFromString`, `TimeFromString`, `DateToString`, `TimeToString`, `GetGenderList`), `PlanUtils` (`FixControls`, `FixDateTime`, `Get/SetListComboValue`), `DialogEditOneSisterGame`; im uses, aber **nicht benutzt**: `EditMandatoryDatesDialog`, `DialogPanelOptionArray`, `DialogForSingleGewichtungsOptions`, `DialogPanel`, `DialogEditTeamName`, `PlanTypes`; VCL: `Grids` (`TStringGrid`), `Samples.Spin` (`TSpinEdit`), `ValEdit`, `Menus`, `Math`
**Verwendet von:** `DialogPanelSisterTeamsDetail` (Neu/Bearbeiten, Z. ~156/176), `DialogPanelLocationsCompactDetail` (Z. ~413)

## Zweck
Modaler Editor für eine **Nachbarmannschaft** (weitere Mannschaft desselben Vereins in einer anderen Liga) inklusive ihrer Begegnungsliste. Nachbarmannschaften beeinflussen Hallenbelegung und parallele Spiele. Der Dialog arbeitet auf einer privaten Kopie eines `TDataObject`-Knotens `nSisterTeam` (`sisterteam`) mit Kindern `sistergame` und schreibt beim Übernehmen zurück.

## Inhalt / Struktur
- Privat: `SisterTeamNode: TDataObject` (Arbeitskopie, `FormCreate`/`FormDestroy`), `MainTeamName`, `OldSisterTeamName`, `PlanData`.
- `SetValues(SisterTeamNode, PlanData, MainTeamName)` – leert Arbeitskopie, `assign` aus Quelle (falls vorhanden), merkt `OldSisterTeamName`, füllt Lokal-Combo, `DataToForm`.
- `DataToForm` – Name, Nummer (`aTeamNumber`), Art (`aGender`, freie Combo mit Vorschlagsliste `GetGenderList`: Herren, Damen, Jungen, …, Seniorinnen 70), Lokal, Checkboxen `aNoParallelGames`, `aParallelHomeGames`; dann `DataToGrid`, `EnableButtons`.
- `DataToGrid` – baut `TStringGrid` neu: Spalten Datum(100px), Uhrzeit(80), Heimmannschaft(200), Gastmannschaft(200), Spiellokal(200). Sister-Team-Namen, die `OldSisterTeamName` entsprechen, werden **live** durch den aktuellen Editnamen ersetzt. Spiellokal: bei Heimspiel des Sister-Teams Default = gewähltes Team-Lokal, überschrieben durch spielbezogenes gültiges Lokal (`FixLocation`). Danach `SortGrid`, Zeilenposition wiederherstellen.
- `SortGrid` – Bubble-Sort über Grid-Zellen nach Datum+Uhrzeit (bei jedem Tausch Neustart bei Zeile 1 → O(n³) im Worst Case; bei kleinen n unkritisch).
- `getSelectedGameNode` – findet den Datenknoten zur markierten Grid-Zeile durch **Textvergleich** von Datum, Zeit, Heim, Gast (letzter Treffer gewinnt).
- `ButtonGameNewClick` – `TDialogEditOneSisterGame`; bei OK neuer Kindknoten `TDataObject.Create(nSisterGame, SisterTeamNode)` (Konstruktor hängt sich selbst an Parent an).
- `ButtonGameEditClick` / `GridDblClick` – Bearbeiten des gewählten Spiels.
- `ButtonGameDeleteClick` – Rückfrage „Möchten Sie das Spiel wirklich löschen?", `RemoveChild`.
- `ButtonOKClick` – Validierung Name/Art nicht leer.
- `GetValues(SisterTeamNode)` – schreibt Formularwerte in die Arbeitskopie, benennt in allen Spielen `OldSisterTeamName` → neuer Name um, dann `SisterTeamNode.assign(Arbeitskopie)`.
- `FormToData` – leer; `ListBoxTeamsClick` – verwaist (keine ListBox im .dfm).

UI (.dfm, Caption „Nachbarmannschaft", `bsSizeToolWin`, 1027×658):
- Kopf (`Panel1`): Mannschaftsname (`EditName`, OnChange→Grid neu), Mannschaftsnummer (`TSpinEdit` 1..100, „1., 2., 3. Mannschaft"), Art (Herren/Damen) `ComboBoxArt` (editierbar), Spiellokal (`csDropDownList`, „leer, falls Verein nur in einem Spiellokal spielt", OnChange→Grid neu), Checkboxen „Gleichzeitige (parallele) Spiele mit dieser Mannschaft vermeiden" und „Gleichzeitige (parallele) Heimspiele mit dieser Mannschaft sind erwünscht".
- Mitte: Label „Begegnungen", `Grid` (RowSelect), rechts Buttons „Neue Begegnung...", „Begegnung bearbeiten...", „Begegnung Löschen...".
- Unten OK/Abbrechen.

## Fachliche Logik / Regeln
- `noparallelgames` = parallele Spiele (gleiche Zeit) zwischen Hauptmannschaft und dieser Nachbarmannschaft vermeiden (z.B. gemeinsame Spieler).
- `parallelhomegames` = gleichzeitige Heimspiele erwünscht (z.B. Damen und Mädchen gemeinsam in der Halle).
- Spiellokal der Nachbarmannschaft gilt als Default für ihre Heimspiele; Einzelspiele können abweichen (Hallenbelegungsberechnung).
- Umbenennung des Sister-Teams propagiert in Heim/Gast der zugehörigen Spiele.

## Daten & Persistenz
Keine direkte. Knoten `sisterteam` (Attribute `teamname`, `teamnumber`, `gender`, `location`, `noparallelgames`, `parallelhomegames`) mit Kindern `sistergame`. Persistenz später über `TPlanData`-XML.

## Threading / Performance
Keine. `SortGrid` quadratisch/kubisch, aber nur Dutzende Zeilen.

## Plattformabhängigkeiten
VCL `TStringGrid`, `TSpinEdit` (Samples), `MessageDlg`, `ShowMessage`.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI` → `EditSisterTeamViewModel` mit `ObservableCollection<SisterGameRowViewModel>` (DataGrid, sortiert per `SortDescription`/LINQ `OrderBy(g => g.DateTime)`), Commands `NewGame`, `EditGame`, `DeleteGame` (CanExecute = Auswahl ≠ null).
- **Selektion über Objektreferenz** statt Textvergleich (`getSelectedGameNode`) – beseitigt Mehrdeutigkeit bei identischen Spielen.
- Modell: `SisterTeam` (Name, Number, Gender, Location, AvoidParallelGames, ParallelHomeGamesWanted, `List<SisterGame>`). Umbenennung: Spiele besser über Rolle (IsHome) statt Namensstring speichern; für XML-Kompatibilität beim Serialisieren Namen einsetzen.
- Arbeitskopie/Assign-Muster → ViewModel hält Kopie (Clone) und gibt sie bei OK zurück.
- Gender-Vorschlagsliste als editierbare ComboBox (`IsEditable=true`, AutoComplete).
- Aufwandsschätzung: **M** – Grid + Unterdialog + Umbenennungslogik.
- Bugs/Auffälligkeiten:
  - Z. 202–205: `Grid.Row := OldRow` nach Löschen der letzten Zeile kann außerhalb des gültigen Bereichs liegen (VCL wirft dann „Grid index out of range") – beim Port Selektion sauber klemmen.
  - Z. 244–268: Auswahl per Textvergleich; zwei identische Spiele → immer das letzte wird bearbeitet/gelöscht.
  - Z. 186–189: Default-Lokal wird im Grid nur angezeigt, wenn `HomeTeamName = Trim(EditName.Text)` – reine Anzeige, nicht gespeichert.
  - `EnableButtons` wird nach `DataToGrid` durch Edit/Löschen nicht erneut aufgerufen (Button-Zustand kann veralten, bis Grid-Klick).
  - Viele ungenutzte Units im uses (Kopplung vermeiden).

## Offene Fragen
- Sollen Sister-Team-Spiele auf Duplikate/Saisonzeitraum geprüft werden?
- Ist `teamnumber` fachlich relevant (Parallelspiel-Logik) oder nur Anzeige?
