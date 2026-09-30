# DialogPanelFreeDays (DialogPanelFreeDays.pas + DialogPanelFreeDays.dfm)

**Kategorie:** UI-Panel (eingebettet) – globales Panel (ganzer Plan)
**Umfang:** 228 Zeilen .pas / 76 Zeilen .dfm
**Abhängigkeiten (uses):** DialogPanel (Basisklasse `TDialogPanel`), PlanDataObjects (`TPlanData.getAllPossibleDates`, `nNoGameDay`, `aDate`), PlanUtils (`MyFormatDate`, `FixControls`), PlanTypes; ungenutzt: EditMandatoryDatesDialog, DialogPanelOptionArray, DialogForSingleGewichtungsOptions; VCL `TListView`
**Verwendet von:** AndiGeneratorMain (Panelliste in `ToolButtonDataClick`)

## Zweck
Pflege der **spielfreien Tage** für den gesamten Plan (z.B. Feiertage, Schulferien-Tage): an den angehakten Tagen sollen keine Spiele stattfinden. Angeboten werden nur Tage, die bei mindestens einer Mannschaft als Wunschtermin (Heimspieltermin) vorkommen.

## Inhalt / Struktur
- `TDialogPanelFreeDays = class(TDialogPanel)` (direkt, **nicht** mannschaftsbezogen).
- UI (.dfm): Caption „Spielfreie Tage“; `PanelTop` mit `Label1` „An diesen Tagen sollen keine Spiele stattfinden:“; `PanelBottom` → `ListViewFreeDates` (`vsReport`, `Checkboxes`, Spalte „Datum“, `OnClick = ListViewFreeDatesClick`).
- `DefaultValues: TList<TDateTime>` – angelegt/freigegeben, **nie benutzt**.
- Methoden:
  - `DateListFromData(List, PlanData)` – alle `plan/nogameday@date`; entfernt Daten, die nicht in `getAllPossibleDates` (Menge aller Wunschtermin-Tage aller Teams, `Trunc`, sortiert) enthalten sind; sortiert.
  - `DateListToForm(List)` – eine Zeile je möglichem Datum (`MyFormatDate` = `'ddd dd.mm.yyyy'`), angehakt wenn in Liste.
  - `FormToDateList(List)` – Index-paralleles Auslesen: Zeile *i* ↔ `getAllPossibleDates[i]` (setzt voraus, dass sich die Datumsmenge seit `DataToForm` nicht geändert hat).
  - `DataToForm`, `FormToData` (`root.deleteChilds(nNoGameDay)`, dann je angehaktem Tag `nogameday date="dd.mm.yyyy"` direkt unter `plan`).
  - `isDefault` – Vergleich mit `PlanDataDefault` (leer wenn nil); **ruft `FormToData()`** (Seiteneffekt).
  - `toDefault` – Default-Daten ins Formular.
  - `setPanel`, `ListViewFreeDatesClick` → `FireOnChange`.

## Fachliche Logik / Regeln
- Spielfreie Tage (global) werden als `nogameday` **direkt unter `plan`** gespeichert – im Unterschied zu **Sperrterminen** einer Mannschaft (`nogameday` unter `team`, siehe HomeDays-Panel). Gleicher Knotenname, unterschiedlicher Kontext.
- Nur Tage mit mindestens einem Wunschtermin sind wählbar; ein globaler freier Tag, der auf keinen Wunschtermin fällt, ist für die Planung ohnehin irrelevant.

## Daten & Persistenz
`plan/nogameday[@date]` (Schlüssel `date`, Format `dd.mm.yyyy`). Persistenz über `*.modifications`-Diff.

## Threading / Performance
Keine. `getAllPossibleDates` wird mehrfach aufgerufen (O(Teams × Termine) mit `IndexOf` → quadratisch), bei Ligagrößen unkritisch.

## Plattformabhängigkeiten
VCL `TListView` mit Checkboxen; Datumsformat über `DateTimeToString` (Wochentagsabkürzung locale-abhängig: `ddd`).

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels.FreeDaysPageViewModel : EditorPageViewModel`; Core: `Plan.FreeDays : SortedSet<DateOnly>`.
- Liste als `ObservableCollection<CheckableDate>` (Datum im Item halten statt Index-Parallelität).
- Datum als `DateOnly`; Anzeige mit fester Kultur `de-DE` (`"ddd dd.MM.yyyy"`).
- **Aufwand:** S.
- **Bugs/Auffälligkeiten:**
  - Z. 80–87 / 135: Freie Tage, die nicht (mehr) auf einem Wunschtermin liegen, werden beim nächsten `FormToData` **stillschweigend gelöscht** (z.B. nachdem Wunschtermine geändert wurden). Bewusst entscheiden, ob das so bleiben soll.
  - `FormToDateList` hängt von identischer Reihenfolge/Anzahl zwischen ListView und `getAllPossibleDates` ab (Z. 157–165); ändert ein anderes Panel zwischenzeitlich Wunschtermine, ist das bei Panelwechsel durch Neuaufbau abgefangen.
  - `DefaultValues` unbenutzt; `isDefault` mit Seiteneffekt.

## Offene Fragen
- Sollen auch Tage außerhalb der Wunschtermine als spielfrei markierbar sein (Kalenderauswahl)?
