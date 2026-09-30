# DialogQuestionHardErrors (DialogQuestionHardErrors.pas + DialogQuestionHardErrors.dfm)

**Kategorie:** UI-Dialog (modal)
**Umfang:** 74 Zeilen .pas / 304 Zeilen .dfm (überwiegend eingebettete Bitmap)
**Abhängigkeiten (uses):** PlanTypes, PlanUtils (`FixControls`); VCL: TMemo, TImage
**Verwendet von:** AndiGeneratorMain (`ToolButtonExportClick`: vor dem CSV-Export)

## Zweck
Warnungsdialog vor dem **Export des Plans (CSV)**, wenn der Plan „harte Fehler" enthält (`TPlan.getHardErrorMessages`). Zeigt die Liste der Probleme und fragt „Möchten Sie den Plan trotzdem exportieren?" (Ja/Nein).

## Inhalt / Struktur
- `function DialogHardErrors(Messages: TStrings): Boolean` – erzeugt Formular (Owner nil), `Memo.Lines := Messages`, `ShowModal`; `True` nur bei `mrOK` (Ja); danach `Form.Release`.
- `ButtonOKClick` → `mrOk`; `ButtonCancelClick` → `mrNo`.
- UI (.dfm): Caption „Warnung...", Label „Folgende Probleme sind in dem Plan enthalten:", `Memo` (721×259, vertikale Scrollbar, **nicht ReadOnly**), `Image1` = eingebettetes **48×48-24-bit-Bitmap** (Warn-/Ausrufezeichen-Symbol, orange/rot, Transparenzfarbe Magenta `FF00FF`), Label „Möchten Sie den Plan trotzdem exportieren?", Buttons „Ja" (Default) / „Nein" (Cancel).

## Fachliche Logik / Regeln
Aufrufer: `if Messages.Count > 0 then if not DialogHardErrors(Messages) then Exit;` → Export abbrechen. Welche Fehler „hart" sind, definiert `TPlan.getHardErrorMessages` (PlanTypes).

## Daten & Persistenz
Keine.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL; Bitmap in DFM-Binärform (muss als PNG/SVG-Ressource extrahiert oder durch Standard-Warnsymbol ersetzt werden); `Release` (Message-basiertes Freigeben).

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Dialogs.ConfirmWithDetailsDialog` (generisch: Titel, Intro, Detailliste, Frage, Ja/Nein) – kann auch für DialogQuestionUseUserOptions genutzt werden.
- Rückgabe `Task<bool>`.
- Memo ReadOnly machen.
- Aufwand: **S**.
- **Auffälligkeiten:** Schließen über X oder Esc → nicht `mrOK` → „Nein" (sicherer Default, korrekt). `Memo` editierbar (kosmetisch).

## Offene Fragen
Keine.
