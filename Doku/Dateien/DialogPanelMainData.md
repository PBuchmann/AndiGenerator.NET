# DialogPanelMainData (DialogPanelMainData.pas + DialogPanelMainData.dfm)

**Kategorie:** UI-Panel (eingebettet)
**Umfang:** 174 Zeilen .pas / 145 Zeilen .dfm
**Abhängigkeiten (uses):** DialogPanel, PlanDataObjects (`TPlanData`, `GetGenderList`, Attribut-Konstanten), PlanUtils (`FixControls`), PlanTypes; weitere uses (EditMandatoryDatesDialog, DialogPanelOptionArray, DialogForSingleGewichtungsOptions) ungenutzt; VCL: TEdit, TComboBox, TDateTimePicker, `ShowMessage`
**Verwendet von:** AndiGeneratorMain (erstes Panel in `TDialogForMultiplePanel`, Menü/Toolbar „Daten")

## Zweck
Seite „**Ligadaten**": Stammdaten der Liga – Name, Liganummer, Art (Herren/Damen/…), Start Vorrunde, Start Rückrunde, Ende Rückrunde. Implementiert den `TDialogPanel`-Vertrag inkl. Validierung (`CheckData`).

## Inhalt / Struktur
- `TDialogPanelMainData = class(TDialogPanel)`
  - `FormCreate` → `GetGenderList(ComboBoxArt.Items)` (Herren, Damen, Jungen, Jungen U18, Mädchen, Mädchen U18, Schüler, Schülerinnen …), `FixControls`.
  - `DataToForm` → liest `root`-Attribute `name`, `gender`, `id`, `from`, `until`, `mid`. Ist `from` = 0 (nicht gesetzt), Vorbelegung: Vorrunde **1.7.** des aktuellen Jahres, Rückrunde **30.12.**, Ende **1.5.** des Folgejahres.
  - `FormToData` → schreibt die sechs Werte zurück (`SetAsString`/`SetAsDate`).
  - `CheckData` → Validierungen (s. u.), Meldung per `ShowMessage`.
  - `isDefault` → Vergleich aller Felder mit `PlanDataDefault.root` (Daten per `Trunc`).
  - `toDefault` → Werte aus `PlanDataDefault`.
  - `ControlChange` → `FireOnChange` (bei Edit/DateTimePicker-Änderung; **nicht** an ComboBoxArt gebunden).
- UI (.dfm): Labels „Name der Liga", „Liganummer", „Art (Herren/Damen)", „Start Vorrunde", „Start Rückrunde*", „Ende Rückrunde", Hinweis „* wird bei Halbrunden ignoriert". Controls `EditName`, `EditId`, `ComboBoxArt` (Freitext möglich, Style csDropDown), 3 × `TDateTimePicker`.

**Muster:** Klassisches Einzel-Panel des Frameworks: Formular mit `PanelMain`, `setPanel` hängt `PanelMain` in den Host, Daten werden per `DataToForm`/`FormToData` zwischen `TPlanData` und Controls gespiegelt.

## Fachliche Logik / Regeln
- Pflichtfelder: Liganame, Liganummer, Art nicht leer (nach `Trim`).
- `Ende Rückrunde > Start Rückrunde > Start Vorrunde` (strikt).
- Start Rückrunde wird bei **Halbrunden** ignoriert (nur UI-Hinweis; Logik woanders).
- Default-Saison: 01.07.–30.12. / 30.12.–01.05.

## Daten & Persistenz
`TPlanData.root`-Attribute: `name`, `gender`, `id`, `from`, `until`, `mid` (Datum). Keine Dateien.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL `TDateTimePicker` (Windows Common Control), `ShowMessage`, `FixControls`.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Panels.MainDataPanelViewModel` + View (Avalonia `CalendarDatePicker`/`DatePicker`).
- `TDateTime` → `DateOnly` (nur Datum relevant). `Trunc(...)` entfällt.
- Validierung → `INotifyDataErrorInfo` oder `Validate(): IReadOnlyList<string>`; Reihenfolge der Meldungen beibehalten (erste Meldung gewinnt).
- Gender-Liste als Konstante in `Core`.
- Aufwand: **S**.
- **Auffälligkeiten:**
  - `ComboBoxArt` hat kein `OnChange` → Änderung der Art aktualisiert den „Standard"-Button nicht sofort.
  - `DataToForm`: Vorbelegung, wenn nur `from` fehlt; fehlen `until`/`mid` bei gesetztem `from`, werden 0-Daten (30.12.1899) angezeigt.
  - Vorbelegung Rückrunde 30.12. liegt in der Praxis meist im Januar – nur Default.

## Offene Fragen
- Ist die Vorbelegung (1.7./30.12./1.5.) weiterhin gewünscht?
