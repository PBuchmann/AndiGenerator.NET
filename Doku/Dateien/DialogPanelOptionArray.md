# DialogPanelOptionArray (DialogPanelOptionArray.pas + DialogPanelOptionArray.dfm)

**Kategorie:** UI-Panel (eingebettet) – wiederverwendbares Steuerelement
**Umfang:** 306 Zeilen .pas / 77 Zeilen .dfm
**Abhängigkeiten (uses):** PlanTypes (`TCalculateOptionsValues`, `cCalculateOptionsNamen`), PlanUtils (`FixControls`); VCL: TScrollBox, TComboBox, TLabel, TButton; `EditMandatoryDatesDialog` ungenutzt
**Verwendet von (tatsächlich instanziiert):** DialogOptions (`PanelPlanOptions`, `PanelMannschaftOptions`), DialogForSingleGewichtungsOptions (`PanelPlanOptions`). Viele weitere Units führen es nur in `uses`.

## Zweck
Dynamisch erzeugte **Liste von Gewichtungs-Auswahlen**: Für jeden Titel (z. B. Kostenart „Hallenbelegung", „60-km-Regel" …) eine Zeile aus Label + ComboBox (Gewichtung „Nicht berücksichtigen" … „extrem hoch") + optionalem Button „Anpassen…". Ist **kein** `TDialogPanel` (erbt direkt von `TForm`), folgt aber dem gleichen Einbettungsmuster (`SetPanel` hängt `PanelMain` um).

## Inhalt / Struktur
- `TOptionButtonClick = procedure(Sender: TObject; Index: Integer) of object` – Event für „Anpassen…"-Button mit Zeilenindex.
- `TFormPanelOptionArray = class(TForm)`
  - Listen: `Combos`, `Labels`, `Buttons: TList<…>`, `VisibleState: TList<Boolean>`; `InLoad`; `FShowButtons`.
  - `Init(titles: TStrings)` → je Titel Label/Combo/Button **zur Laufzeit erzeugen**; Positionen/Größen werden von unsichtbaren „Position-Helper"-Controls aus der .dfm abgeleitet (`LabelPositionHelperFirst/Second`, `ComboPositionHelperFirst`, `ButtonPositionHelper`). Zeilenabstand = `LabelPositionHelperSecond.Top − LabelPositionHelperFirst.Top` (27 px bei 96 dpi).
  - `FillCombo` → Einträge aus `cCalculateOptionsNamen` = 'Nicht berücksichtigen', 'sehr wenig', 'wenig', 'normal', 'hoch', 'sehr hoch', 'extrem hoch'; Vorauswahl Index 3 (= `coNormal`).
  - `ReArangeCombos` → setzt Top/Visible entsprechend `VisibleState` und `ShowButtons` (unsichtbare Zeilen erzeugen keine Lücke).
  - `SetValues(List<TCalculateOptionsValues>)` / `GetValues(...)` → ItemIndex ↔ `Ord(enum)`.
  - `SetVisibleState(List<Boolean>)` → nur bei Änderung neu anordnen.
  - `toDefault` → alle auf `coNormal`; `isDefault` → alle `coNormal`?
  - `OnChanged` (bei Combo-Klick, nicht während `InLoad`), `OnButtonClick(Sender, Index)`.
  - Property `ShowButtons`.
- UI (.dfm): `PanelMain: TScrollBox` (alClient, ohne Rahmen) mit den unsichtbaren Helper-Controls.

## Fachliche Logik / Regeln
- Gewichtungsstufen `TCalculateOptionsValues = (coIgnore, coSehrWenig, coWenig, coNormal, coHoch, coSehrHoch, coExtremHoch)`; zugeordnete Faktoren (PlanTypes `cCalculateOptionsValues`): **0, 1, 10, 100, 1000, 10000, 100000** – logarithmische Skala. Default = `coNormal` (100).

## Daten & Persistenz
Keine – Werte werden vom Aufrufer (DialogOptions) gelesen/geschrieben.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
Laufzeit-Erzeugung von VCL-Controls mit absoluter Pixel-Positionierung; „Position-Helper"-Trick; TScrollBox.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Controls.WeightingOptionList` (UserControl) + `ObservableCollection<WeightingOptionItemVm { string Title; Weighting Value; bool IsVisible; ICommand AdjustCommand }>` – `ItemsControl` mit DataTemplate (Grid: Label | ComboBox | Button). Keine absolute Positionierung nötig.
- `TCalculateOptionsValues` → `enum Weighting { Ignore, VeryLow, Low, Normal, High, VeryHigh, ExtremelyHigh }` in Core + Anzeigenamen-Tabelle (deutsch).
- Aufwand: **S**.
- **Auffälligkeiten:**
  - `FillCombo` setzt `ItemIndex := 3` hart kodiert statt `Ord(coNormal)` – bei Enum-Änderung fehleranfällig.
  - `SetValues`/`SetVisibleState` setzen voraus, dass die Liste nicht länger als die Anzahl Zeilen ist (sonst Index-Exception).
  - Button-Tab-Reihenfolge wird durch `FixControls` nur beim Create (vor `Init`) berechnet → dynamische Controls in Erstellungsreihenfolge.

## Offene Fragen
Keine.
