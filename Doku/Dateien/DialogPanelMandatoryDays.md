# DialogPanelMandatoryDays (DialogPanelMandatoryDays.pas + DialogPanelMandatoryDays.dfm)

**Kategorie:** UI-Panel (eingebettet)
**Umfang:** 337 Zeilen .pas / 96 Zeilen .dfm
**Abhängigkeiten (uses):** DialogPanel, EditMandatoryDatesDialog (`EditMandatoryDate`), PlanTypes (`TPlanMandatoryTime`, `TPlanMandatoryTimeList`), PlanDataObjects, PlanUtils (`MyFormatDate`, `FixControls`); VCL: `TValueListEditor`, `TPopupMenu`, WinAPI `GetCursorPos`
**Verwendet von:** AndiGeneratorMain (Daten-Dialog), DialogFirstStart (Einrichtungs-Assistent, `StartAction(TDialogPanelMandatoryDays)`)

## Zweck
Seite „**Pflichtspieltage**": Zeiträume, in denen **jede Mannschaft mindestens n Spiele** absolvieren muss (z. B. „bis 15.10. mindestens 2 Spiele"). Liste mit Zeitraum und Mindestanzahl; Anlegen/Bearbeiten über `EditMandatoryDatesDialog`, Löschen über Kontextmenü; Mindestanzahl zusätzlich direkt im Grid per Picklist (1–10) änderbar.

## Inhalt / Struktur
- Private Felder: `DefaultMandatoryGameList` (ungenutzt, nur erzeugt/freigegeben), `workMandatoryGameList` (Arbeitskopie), `mapRowToMandatoryGameList: TDictionary<Integer, TPlanMandatoryTime>` (Grid-Zeile → Objekt), `InLoad` (unterdrückt Change-Events beim Befüllen).
- `DateListFromData(List, PlanData)` → alle `mandatorygames`-Kinder von `root` → `TPlanMandatoryTime(DateFrom, DateTo, GameCount)`, anschließend `Sort`.
- `DateListToForm(List)` → Grid neu aufbauen: Key = `"<von> - <bis>"` (`MyFormatDate`), Value = GameCount; Zeile ReadOnly (Key), Value mit `esPickList` 1..10; leere Liste → Platzhalterzeile „Keine Pflichtspieltermine vorhanden".
- `FormToDateList` → liest GameCount aus Grid-Spalte 1 zurück in die Objekte (`StrToInt`), kopiert `workMandatoryGameList`.
- `DataToForm` / `FormToData` (löscht alle `mandatorygames`-Kinder und schreibt neu) / `toDefault` / `isDefault` (Liste aus Default vs. aus PlanData **nach `FormToData()`**, elementweise `IsTheSame`).
- Kontextmenü (`ValueListMandatoryDatesMouseDown` bei Rechtsklick): „Neu…" (Default: heute–heute, 1 Spiel), „Bearbeiten…", „Löschen…" (letztere nur auf Datenzeilen aktiviert). Menü wird an `GetCursorPos` geöffnet.
- `FormCreate`: DPI-Skalierung der Zeilenhöhe `MulDiv(DefaultRowHeight, Screen.PixelsPerInch, 96)`.
- UI (.dfm): Hinweistext „In diesen Zeiträumen müssen die Mannschaften mindestens eine bestimmte Anzahl Spiele machen (Maus rechts für Löschen und Hinzufügen)", `ValueListMandatoryDates` (Spalten „Zeitraum", „Anzahl Spiele"). Form-Caption „Plichtspieltage" (Tippfehler).

## Fachliche Logik / Regeln
- Pflichtspielzeitraum: [DateFrom, DateTo] (ganztägig, `Trunc`), Mindestanzahl Spiele 1–10 (UI-Grenze). Kostenfunktion: `mktMandatoryGames` (in PlanTypes).
- Validierung „bis ≥ von" im Edit-Dialog.

## Daten & Persistenz
`<mandatorygames datefrom=… dateto=… numbergames=…/>` (mehrfach) unter `root` des `TPlanData`.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
`TValueListEditor` (VCL-spezifisch), `GetCursorPos` (WinAPI), `Screen.PixelsPerInch`, Popup-Menü.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Panels.MandatoryDaysPanelViewModel` mit `ObservableCollection<MandatoryTimeVm>`; View als `DataGrid` (Avalonia) mit ComboBox-Spalte 1–10 und `ContextMenu` bzw. Buttons.
- `TPlanMandatoryTime` → `record MandatoryTime(DateOnly From, DateOnly To, int GameCount)` in `AndiGenerator.Core`.
- Row→Objekt-Dictionary entfällt durch Datenbindung.
- `isDefault` darf in C# keine Seiteneffekte haben (Original ruft `FormToData()` – schreibt bei jeder Änderung in PlanData; funktional harmlos, da Host-Kopie).
- Aufwand: **S–M**.
- **Auffälligkeiten:**
  - `DefaultMandatoryGameList` ungenutzt.
  - `ListViewFreeDatesClick` ist Copy-&-Paste-Rest (nicht verdrahtet).
  - `FormToDateList` (Z. 190): `StrToInt` ohne Fehlerbehandlung. Die Wertzelle ist `ItemProp.ReadOnly = True` mit `esPickList` – ob der Benutzer den Wert damit im Grid tatsächlich ändern kann (nur über Picklist) oder nur über den Bearbeiten-Dialog, ist im Code nicht eindeutig (VCL-Verhalten, am Original prüfen).
  - `DateListToForm` ruft `workMandatoryGameList.Assign(DateList)` auch wenn `DateList = workMandatoryGameList` (Neu/Bearbeiten/Löschen); `TPlanMandatoryTimeList.Assign` (PlanTypes Z. 9209) fängt Selbstzuweisung ab (`if Self <> Source`) → korrekt. Folge: neue/bearbeitete Einträge werden **nicht neu sortiert**, bis die Seite neu geladen wird (Sortierung nur in `DateListFromData`).
  - `TPlanMandatoryTimeList` ist `TObjectList` mit `OwnsObjects=True` → `Remove` in `PopupMenuDeleteClick` gibt das Objekt frei; das Dictionary wird direkt danach neu aufgebaut – OK.

## Offene Fragen
- Ist die Obergrenze 10 Spiele fachlich gewollt?
