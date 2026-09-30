# DialogForSinglePanel (DialogForSinglePanel.pas + DialogForSinglePanel.dfm)

**Kategorie:** UI-Dialog (Framework-Host)
**Umfang:** 111 Zeilen .pas / 79 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `DialogPanel` (`TDialogPanel`, `TDialogPanelClass`), `PlanDataObjects` (`TPlanData`), `PlanUtils` (`FixControls`), `PlanTypes`; VCL: `Forms`, `StdCtrls`, `ExtCtrls`
**Verwendet von:** `DialogFirstStart` (`StartAction`, Z. 118); außerdem im uses von `AndiGeneratorMain` (dort keine Instanziierung gefunden).

## Zweck
Host-Fenster für **genau ein** `TDialogPanel` – die Einseiten-Variante des Dialog-Frameworks (ausführlich beschrieben in `DialogForMultiplePanel.md`, Abschnitt „Das Dialog-Framework"). Wird vom Erststart-Assistenten genutzt, um gezielt eine Datenseite (Spiellokale, Koppel, Auswärtskoppel, Setzliste, Pflichtspieltage) zu öffnen.

## Inhalt / Struktur
- Felder: `PlanData` (eigene Arbeitskopie), `DialogPanel`.
- `SetValues(SourcePlan, PlanDataDefault, PanelClass)`:
  - `PlanData := TPlanData.Create; PlanData.Assign(SourcePlan)` (tiefe Kopie)
  - `DialogPanel := PanelClass.Create(Self)`; `setPanel(PanelMain)`; `setPlanData(PlanData, PlanDataDefault)`; `DataToForm`
  - Fenstertitel = `DialogPanel.Caption`; `OnChange` → `EnableButtons`.
- `getPlanData` – liefert die Arbeitskopie (Besitz beim Dialog; Aufrufer muss vor `Free` per `Assign` übernehmen).
- `ButtonOKClick` – `DialogPanel.FormToData`, `mrOk` (**ohne** `CheckData`!).
- `ButtonToStandardClick` – `toDefault`, `EnableButtons`.
- `EnableButtons` – „Standard wiederherstellen" aktiviert ⇔ `not DialogPanel.isDefault`.
- `FormDestroy` – `PlanData.Free`.

UI (.dfm): `bsSizeToolWin`, 950×577, `PanelMain` (alClient, Host), `PanelBottom` mit „Standard wiederherstellen" (links), OK (Default), Abbrechen (Cancel).

## Fachliche Logik / Regeln
Keine eigene.

## Daten & Persistenz
Keine direkte; In-Memory-Kopie von `TPlanData`.

## Threading / Performance
Keine. Eine tiefe Kopie `TPlanData.Assign` beim Öffnen.

## Plattformabhängigkeiten
VCL-Reparenting des `PanelMain` aus dem Panel-Formular.

## Migrationshinweise für C#
- In C# kein eigener Typ nötig: `MultiPageEditorViewModel` mit einer Seite und ausgeblendeter Navigation, oder schlanker `SingleEditorViewModel : EditorHostViewModelBase` (gemeinsame Basis mit Multi: Arbeitskopie, OK/Cancel/Reset-Commands).
- Unterschiede zum Multi-Host bewusst vereinheitlichen:
  - Multi prüft `CheckData` vor OK, Single nicht → in C# immer `Validate()` vor Übernahme.
  - Multi blendet den Reset-Button aus, wenn `PlanDataDefault = nil`; Single nicht (Reset mit `nil`-Default hängt dann von der Panel-Implementierung ab).
- Aufwandsschätzung: **S** (als Teil des Framework-Aufwands).
- Auffälligkeiten/Bugs: Z. 55–59 fehlende `CheckData`-Prüfung bei OK (Inkonsistenz zu `DialogForMultiplePanel` Z. 64); `EnableButtons` berücksichtigt nicht `PlanDataDefault = nil` (Z. 74–77).

## Offene Fragen
- Ist das Weglassen von `CheckData` im Einzel-Host Absicht? (Empfehlung: Validierung vereinheitlichen.)
