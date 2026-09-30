# EditMandatoryDatesDialog (EditMandatoryDatesDialog.pas + EditMandatoryDatesDialog.dfm)

**Kategorie:** UI-Dialog (modal)
**Umfang:** 83 Zeilen .pas / 113 Zeilen .dfm
**Abhängigkeiten (uses):** PlanTypes (`TPlanMandatoryTime`), PlanUtils (`FixControls`); VCL: TDateTimePicker, TComboBox, `ShowMessage`
**Verwendet von:** DialogPanelMandatoryDays (`EditMandatoryDate` in „Neu…"/„Bearbeiten…"). Zahlreiche weitere Units führen die Unit nur in der `uses`-Klausel (Copy-&-Paste, ohne Aufruf): DialogEditSisterTeam, DialogOptions, DialogPanel60km, DialogPanelAuswaertskoppel, DialogPanelFreeDays, DialogPanelHomeCoupleCompact, DialogPanelHomeDays, DialogPanelHomeRight, DialogPanelLocationsCompact, DialogPanelMainData, DialogPanelOptionArray, DialogPanelPredefinedGames, DialogPanelSisterTeams(+Detail), DialogPanelTeams.

## Zweck
Kleiner modaler Dialog „**Pflichtspieltag**" zum Erfassen/Ändern eines Pflichtspielzeitraums: Datum von, Datum bis, minimale Anzahl Spiele (1–10).

## Inhalt / Struktur
- `function EditMandatoryDate(Value: TPlanMandatoryTime): boolean` – Fassade: erzeugt Formular, befüllt `DateTimePicker1/2` mit `DateFrom/DateTo`, `ComboBox1.ItemIndex` = Index von `IntToStr(GameCount)`; bei `mrOk` Rückschreiben (`Trunc` der Daten, `StrToInt` des Combo-Texts) → `True`, sonst `False`. Objekt wird **in-place** geändert.
- `TFormEditMandatoryDatesDialog.ButtonOKClick` → Validierung: `Trunc(von) > Trunc(bis)` → Meldung „Das "bis" Datum darf nicht kleiner als das "von" Datum sein", sonst `ModalResult := mrOk`.
- UI (.dfm): Labels „von:", „bis:", „Minimale Anzahl Spiele:", 2 × `TDateTimePicker`, `ComboBox1` (csDropDownList, Items '1'..'10', Default '1'), unten OK (Default) / Abbrechen (Cancel, ModalResult=2).

## Fachliche Logik / Regeln
- Zeitraum inklusive; von ≤ bis (gleicher Tag erlaubt).
- Mindestanzahl Spiele 1..10.

## Daten & Persistenz
Keine (nur Objekt im Speicher).

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL-Formular, `TDateTimePicker`, `ShowMessage`.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Dialogs.EditMandatoryDateDialog` (Avalonia `Window` + ViewModel) oder Inline-Editing im DataGrid (dann entfällt der Dialog).
- Rückgabe als `Task<MandatoryTime?>` (immutable record) statt In-place-Mutation.
- Aufwand: **S**.
- **Auffälligkeiten:** Wenn `GameCount` außerhalb 1..10 liegt (z. B. aus importierter Datei), ist `ItemIndex = -1` → bei OK `Items[-1]` → Exception (Z. 48/54).

## Offene Fragen
Keine.
