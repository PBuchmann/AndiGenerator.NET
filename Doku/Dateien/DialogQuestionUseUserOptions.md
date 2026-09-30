# DialogQuestionUseUserOptions (DialogQuestionUseUserOptions.pas + DialogQuestionUseUserOptions.dfm)

**Kategorie:** UI-Dialog (modal)
**Umfang:** 72 Zeilen .pas / 59 Zeilen .dfm
**Abhängigkeiten (uses):** PlanTypes, PlanUtils (`FixControls`); Winapi.Windows (`IDNO`); VCL: TMemo
**Verwendet von:** AndiGeneratorMain (beim Laden eines Plans, ca. Z. 780–800)

## Zweck
Rückfrage beim **Laden eines Plans**: Für diesen Plan wurden bei der letzten Verwendung vom Standard abweichende Benutzereinstellungen (Optimierungs-Gewichtungen) gespeichert. Der Dialog listet die Abweichungen und fragt, ob sie wiederverwendet oder verworfen werden sollen.

## Inhalt / Struktur
- `function DialogUseOptionsQuestion(Messages: TStrings): Boolean` – Formular (Owner nil), `Memo.Lines := Messages`, `ShowModal`; Ergebnis `True`, **wenn nicht `IDNO`** zurückkam; `Form.Release`.
- Klasse heißt `TFormQuestionUseUserSettings` (≠ Unit-Name).
- `ButtonOKClick` → `mrOk`; `ButtonCancelClick` → `mrNo`.
- UI (.dfm): Caption „Benutzereinstellungen...", Label „Bei der letzten Verwendung wurden zu diesem Plan folgende Benutzereinstellungen hinterlegt:", `Memo` (721×262, nicht ReadOnly), Buttons „Ja, diese Einstellung wieder verwenden" (Default) / „Nein, die Standardeinstellungen verwenden" (Cancel).

## Fachliche Logik / Regeln
Aufrufer (AndiGeneratorMain):
```
PlanLoaded.getOptions().Load(PlanLoaded, CurrentPlanDirectory + '\AndiGenerator.options');
PlanLoaded.getOptions().getDiffToDefault(PlanLoaded, Messages);
if WithLoadMessages and (Messages.Count > 0) and not DialogUseOptionsQuestion(Messages) then
  Options.Clear; Options.Save(... 'AndiGenerator.options');
```
→ „Nein" setzt die Optionen zurück **und überschreibt die Optionsdatei** sofort. Die Messages stammen aus `getDiffToDefault` (z. B. „Überlappung der Spieltage: hoch").

## Daten & Persistenz
Keine direkt; indirekt `…\<Planverzeichnis>\AndiGenerator.options` (durch Aufrufer).

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL; Vergleich mit WinAPI-Konstante `IDNO` (= 7 = `mrNo`).

## Migrationshinweise für C#
- Ziel: gleicher generischer `ConfirmWithDetailsDialog` wie bei DialogQuestionHardErrors; Rückgabe `Task<bool>`.
- Pfad-Separator: Aufrufer nutzt `'\'` → in C# `Path.Combine`.
- Aufwand: **S**.
- **Auffälligkeiten:** Schließen per Fenster-X liefert `mrCancel` (≠ `IDNO`) → wird als **„Ja, wiederverwenden"** gewertet; Esc löst den Cancel-Button aus → „Nein". Uneinheitlich; in C# bewusst festlegen (empfohlen: X = Ja, da nicht-destruktiv – so wie Original).

## Offene Fragen
- Soll „Nein" die gespeicherte Optionsdatei weiterhin sofort überschreiben?
