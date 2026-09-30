# DialogPrintSelect (DialogPrintSelect.pas + DialogPrintSelect.dfm)

**Kategorie:** UI-Dialog (modal) / Druck
**Umfang:** 59 Zeilen .pas / 151 Zeilen .dfm
**Abhängigkeiten (uses):** PlanTypes, PlanUtils (`FixControls`); VCL-Standard
**Verwendet von:** PlanPanel (Druckfunktion, ca. Z. 1230–1300: `TFormPrintSelect.Create`, liest Checkboxen und `ComboBoxFormat`)

## Zweck
Modaler Auswahldialog „**Druckauswahl**": Welche Teile des Spielplans sollen gedruckt werden, und in welchem Seitenformat. Enthält keinerlei Logik außer OK/Abbruch; der Aufrufer liest die Controls direkt aus.

## Inhalt / Struktur
- `TFormPrintSelect` – `ButtonOKClick` → `mrOk`, `ButtonCancelClick` → `mrCancel`, `FormCreate` → `FixControls`.
- UI (.dfm), alle Checkboxen standardmäßig **aktiviert**:
  - „Terminwünsche" (`CheckBoxTerminWuensche`)
  - „Termine der Nachbarmannschaften" (`CheckBoxSisterTeams`)
  - „Kosten" (`CheckBoxKosten`)
  - „Spielplan" (`CheckBoxTerminPlan`)
  - „Mannschaftspläne" (`CheckBoxMannschaftsplaene`)
  - „Mannschaftspläne mit Nachbarmannschaften" (`CheckBoxMannschaftsplaeneWithSisterTeams`)
  - „Diagramme" (`CheckBoxVerteilung`)
  - „Seitenformat:" `ComboBoxFormat` = Hochformat (Index 0, Default) / Querformat (1)
  - Buttons OK (Default) / Abbruch (Cancel).
- Nutzung im Aufrufer (PlanPanel): Querformat → `Printer.Zoom := 0.60`, Hochformat → `0.45`; `BeginPrint(PlanName, Querformat)`; dann je Checkbox ein Druckabschnitt.

## Fachliche Logik / Regeln
Keine (reine Auswahl).

## Daten & Persistenz
Keine; Auswahl wird nicht gespeichert (bei jedem Öffnen alles angehakt).

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL-Formular. Der eigentliche Druck (PlanPanel/`TSimplePrinter`, GDI) ist stark Windows-spezifisch – siehe dortige Doku.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Dialogs.PrintSelectDialog` → liefert `record PrintSelection(bool Wishes, bool SisterTeams, bool Costs, bool Schedule, bool TeamPlans, bool TeamPlansWithSisterTeams, bool Charts, bool Landscape)`.
- Empfehlung Cross-Platform: statt Drucker-API **PDF-Export** (z. B. QuestPDF/PdfSharpCore) und Druck über das OS; der Dialog bleibt dann als Export-Auswahl.
- Aufwand: **S** (Dialog); Druck selbst separat (L).

## Offene Fragen
- Soll die Auswahl persistiert werden?
- Direkter Druck oder PDF-Export auf allen Plattformen?
