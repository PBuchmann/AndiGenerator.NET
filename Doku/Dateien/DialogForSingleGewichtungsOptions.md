# DialogForSingleGewichtungsOptions (DialogForSingleGewichtungsOptions.pas + DialogForSingleGewichtungsOptions.dfm)

**Kategorie:** UI-Dialog
**Umfang:** 171 Zeilen .pas / 196 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `DialogPanelOptionArray` (`TFormPanelOptionArray`, `TOptionButtonClick`), `PlanTypes` (`TCalculateOptionsValues`), `PlanUtils` (`FixControls`), `DialogPanel` (ungenutzt); VCL: `Forms`, `StdCtrls`, `ExtCtrls`
**Verwendet von:** tatsächlich instanziiert in `DialogOptions` (Z. ~455, ~506 – Mannschafts-Gewichtungen, verschachtelt mit Button je Zeile) und `PlanPanel` (Z. ~3211, 3254, 3304, 3357 – Klick auf Kostenbereiche im Plan). Im uses (ungenutzt) von ca. 15 weiteren Units (`DialogEditSisterTeam`, `DialogPanel60km`, `DialogPanelHomeDays`, …).

## Zweck
Modaler Dialog zum Bearbeiten einer **Liste von Gewichtungen** (Kostenfaktoren der Optimierung, Werte `TCalculateOptionsValues`). Er ist ein dünner Host um das eingebettete Panel `TFormPanelOptionArray`, das für jeden Titel eine Zeile „Label + ComboBox (+ optional Button)" erzeugt. Optional wird eine Meldungsliste (z.B. Begründungen/Kosten-Details) angezeigt.

## Inhalt / Struktur
- Privat `PanelPlanOptions: TFormPanelOptionArray` – in `FormCreate` erzeugt, `SetPanel(PanelMain)` (Reparenting), `OnChanged → ControlChanged → EnableButtons`.
- Öffentliche API (reine Delegation an das Panel):
  - `Init(titles: TStrings)` – Zeilen anlegen.
  - `SetValues(values: TList<TCalculateOptionsValues>)` / `GetValues(values)`.
  - `SetVisibleState(values: TList<boolean>)` – Zeilen aus-/einblenden (z.B. nur Kostenarten, für die der Plan Daten hat).
  - `ShowButtons` (Property) / `OnButtonClick: TOptionButtonClick(Sender, Index)` – je Zeile ein Button (z.B. Unterdialog für Mannschaftsdetails).
  - `SetTitle(value)` – `LabelTitle` und Fenster-Caption.
  - `SetMessages(values: TStrings)` – blendet `PanelMessages` mit `MemoMessages` ein und setzt `Height := Width*2/3`.
  - `SmallDlg()` – `Height := Width div 2` (für Einzelwert-Dialoge).
- `ButtonToStandardClick` → `PanelPlanOptions.toDefault` (alle Combos auf `coNormal`); `EnableButtons` → Button aktiv ⇔ nicht alle `coNormal`.
- OK → `mrOk`, Abbrechen → `mrCancel` (keine Validierung).

UI (.dfm, `bsSizeToolWin`, 487×590): `Panel1` oben mit `LabelTitle`; `PanelMiddle` mit `PanelSpacer` (links, Einrückung) und `PanelMain` (Host); `PanelMessages` (unten, anfangs unsichtbar) mit Label „Meldungen:" und `MemoMessages`; `PanelBottom` mit „Standard wiederherstellen", OK, Abbrechen.

## Fachliche Logik / Regeln
- Gewichtungsstufen `TCalculateOptionsValues = (coIgnore, coSehrWenig, coWenig, coNormal, coHoch, coSehrHoch, coExtremHoch)`; **Standard = `coNormal`**.
- Typische Nutzung: `PlanPanel` – Gewichtung einer Plan-Kostenart (`cPlanKostenTypeNamen[KostenType]`, `getOptions.getGewichtungPlan/setGewichtungPlan`) bzw. mannschaftsbezogener Kostenarten; `DialogOptions` – pro Mannschaft alle `TMannschaftsKostenType` (`GewichtungMannschaften.getDetail(teamId, KostenType)`), sichtbar nur, wenn `Plan.HasValuesForType`. Danach `DoAfterMainPlanChanged(True)` (nur Optionen speichern).

## Daten & Persistenz
Keine direkte. Werte gehen über den Aufrufer in die Plan-Optionen (`AndiGenerator.options`).

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL-Reparenting eines Panels aus fremdem `TForm`; Höhenberechnung in Pixeln.

## Migrationshinweise für C#
- Ziel: `WeightingDialogViewModel` mit `ObservableCollection<WeightingRowViewModel>` (Title, `Weighting Value`, `bool IsVisible`, optional `ICommand DetailCommand`), `string Title`, `IReadOnlyList<string>? Messages`; Commands OK/Cancel/ResetToDefault (CanExecute: `Rows.Any(r => r.Value != Weighting.Normal)`).
- `enum Weighting { Ignore, VeryLow, Low, Normal, High, VeryHigh, ExtremelyHigh }` (Ordinalwerte beibehalten, da in Options-Datei gespeichert).
- Das eingebettete `TFormPanelOptionArray` wird zu einem wiederverwendbaren `UserControl` (`WeightingList`) + ViewModel; wird auch direkt in `DialogOptions` genutzt.
- `SmallDlg`/`SetMessages`-Größenlogik → `SizeToContent` in Avalonia.
- Aufwandsschätzung: **S** (Dialog) – Hauptaufwand liegt im OptionArray-Panel.
- Auffälligkeiten: `isDefault` prüft auch unsichtbare Zeilen (bei `SetVisibleState` ausgeblendete Werte ≠ Normal lassen den Reset-Button aktiv, ohne dass der Nutzer sieht warum) – Verhalten im Panel, beim Port entscheiden. Viele unnötige uses-Referenzen auf diese Unit.

## Offene Fragen
- Soll „Standard wiederherstellen" auch ausgeblendete Zeilen zurücksetzen (aktuell: ja, alle Combos)?
