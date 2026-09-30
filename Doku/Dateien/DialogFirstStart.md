# DialogFirstStart (DialogFirstStart.pas + DialogFirstStart.dfm)

**Kategorie:** UI-Dialog (Assistent)
**Umfang:** 208 Zeilen .pas / 93 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `DialogForSinglePanel`, `DialogPanel` (`TDialogPanelClass`), `DialogPanelLocationsCompact`, `DialogPanelHomeCoupleCompact`, `DialogPanelAuswaertskoppel`, `DialogPanelRanking`, `DialogPanelMandatoryDays`, `DialogPanelHomeDays` (ungenutzt), `DialogPanelHomeDaysDetail` (ungenutzt), `PlanDataObjects` (`TPlanData`), `PlanTypes` (`TPlan`), `PlanUtils` (`FixControls`, `cNewLine`); im implementation-Teil `AndiGeneratorMain` (`DoAfterMainPlanChanged` – zirkuläre Abhängigkeit zur Hauptform)
**Verwendet von:** `AndiGeneratorMain` (Plan laden, Z. ~843–849)

## Zweck
**Erststart-Assistent** nach dem erstmaligen Import einer click-TT-Datei (keine Modifikationsdatei vorhanden). Er führt den Staffelleiter nacheinander durch Hinweise zu Daten, die click-TT nicht (vollständig) liefert oder die häufig fehlerhaft sind, und öffnet auf Wunsch die passende Datenseite über `TDialogForSinglePanel`. Schritte ohne relevante Daten werden übersprungen.

## Inhalt / Struktur
- `TMessages = (tmLocations, tmKoppel, tmAuswaertskoppel, tmSetzliste, tmMandatoryDays)` – Schrittfolge.
- Felder: `CurrMessage`, `PlanData` (Live-Daten des Hauptfensters!), `PlanDataDefault` (click-TT-Original), `PlanLoaded: TPlan` (für Has…-Abfragen).
- `SetValue(PlanData, PlanDataDefault, PlanLoaded)`.
- `UpdateTexts` – setzt `LabelHeader`, `LabelMessage` und Beschriftung des Aktions-Buttons je Schritt (Texte s.u.).
- `HasData(step)` – ob ein Schritt angezeigt wird:
  - Spiellokale: immer; Koppel: `PlanLoaded.HasPossibleKoppelTermine`; Auswärtskoppel: `PlanLoaded.HasAuswaertsKoppelTermine`; Setzliste: immer; Pflichtspieltage: `PlanLoaded.HasMandatoryGameDays`.
- `NextMessage` – rekursiv zum nächsten Schritt mit Daten; nach dem letzten `ModalResult := mrOk` (Dialog schließt).
- `StartAction(PanelClass)` – öffnet `TDialogForSinglePanel` mit `PlanData`/`PlanDataDefault`; bei OK `PlanData.Assign(Form.getPlanData())` und `DoAfterMainPlanChanged(false)` (Hauptfenster: Plan neu aufbauen, speichern).
- `ButtonOKClick` – Schritt → Panelklasse: Lokale → `TDialogPanelLocationsCompact`, Koppel → `TDialogPanelHomeCoupleCompact`, Auswärtskoppel → `TDialogPanelAuswaertskoppel`, Setzliste → `TDialogPanelRanking`, Pflichtspieltage → `TDialogPanelMandatoryDays`. Nur bei Erfolg (OK im Unterdialog) weiter zum nächsten Schritt; bei Abbruch bleibt der Schritt stehen.
- `ButtonCancelClick` („Überspringen") → `NextMessage`.
- `FormCreate` → Start bei `tmLocations`, `UpdateTexts`, `FixControls`.

UI (.dfm, Caption „Erster Start...", 479×295): `LabelHeader` (fett, 18 px), `LabelMessage` (WordWrap), Buttons Aktion (breit, Default, Beschriftung dynamisch) und „Überspringen" (Cancel=True → auch ESC überspringt, schließt nicht!).

### Texte (fachlich relevant, sinngemäß)
- **Spiellokale** – „Spiellokale werden von click-TT über die Schnittstelle nicht übertragen"; bei Vereinen mit mehreren Lokalen müssen sie bei Mannschaften und Nachbarmannschaften gepflegt werden, sonst ist die Hallenbelegung falsch; überspringbar, wenn alle nur ein Lokal haben oder keine Heimspielbeschränkungen angegeben sind. Button „Spiellokale bearbeiten".
- **Koppeltermine** – Plan enthält Koppelwünsche/mögliche Doppelspieltage; click-TT kann nicht alle Optionen abbilden, Eingabefehler häufig → prüfen. „Koppeltermine bearbeiten".
- **Auswärtskoppelwünsche** – analog. „Auswärtskoppelwünsche bearbeiten".
- **Setzliste** – erwartete Abschlusstabelle vorgeben; Ziel: stärkste/schwächste Mannschaften spielen am Saisonende gegeneinander (Spannung, gegen „Mauscheleien"). „Setzliste aktivieren".
- **Pflichtspieltage** – problematisch v.a. bei ungerader Mannschaftszahl (eine Mannschaft müsste zwei Spiele in einer Woche machen); Empfehlung: Zeitraum verlängern oder Pflichtspieltage löschen und stattdessen den letzten Spieltag optimieren. „Pflichtspieltage bearbeiten".

## Fachliche Logik / Regeln
- Auslöser (im Aufrufer): `WithLoadMessages and FirstStart`, wobei `FirstStart` = click-TT-Datei geladen und keine Modifikationsdatei (`cModificationExtension`) existiert.
- Reihenfolge fest: Lokale → Koppel → Auswärtskoppel → Setzliste → Pflichtspieltage.
- Änderungen werden **sofort** (pro Schritt) in die Hauptdaten übernommen und über `DoAfterMainPlanChanged` weiterverarbeitet – nicht erst am Ende.

## Daten & Persistenz
Indirekt über `DoAfterMainPlanChanged(false)` (Hauptform speichert Plan-/Modifikationsdaten). Keine eigenen Dateien.

## Threading / Performance
Keine. Hinweis: `HasData` fragt `PlanLoaded` ab, das beim Aufruf den Stand *vor* Änderungen im Assistenten widerspiegelt (sofern `DoAfterMainPlanChanged` `PlanLoaded` nicht neu aufbaut – offene Frage).

## Plattformabhängigkeiten
Nur VCL-Formular. Zirkulärer Unit-Bezug auf `AndiGeneratorMain` (globale Funktion auf globaler Hauptform-Instanz).

## Migrationshinweise für C#
- Ziel: `FirstStartWizardViewModel` in `AndiGenerator.UI`, Schritte als Liste `WizardStep(Title, Message, ActionText, Func<bool> hasData, Type pageType)`; Navigation über Index statt Rekursion.
- Aufruf der Seiten über den gemeinsamen Editor-Host (`IDialogService.ShowEditor(pageFactory, work, defaults)`), keine direkte Abhängigkeit zur Hauptform: stattdessen Callback/Event `PlanDataChanged` bzw. `IPlanSession.ApplyChanges(saveOnlyOptions:false)`.
- Texte als Ressourcen (`.resx`) übernehmen.
- ESC-Verhalten (überspringen statt schließen) bewusst nachbilden oder ändern.
- Aufwandsschätzung: **S** – einfacher Zustandsautomat; Abhängigkeiten zu fünf Panels müssen existieren.
- Auffälligkeiten: `DialogPanelHomeDays`/`DialogPanelHomeDaysDetail` ungenutzt importiert; Fenster schließen über [X] beendet den Assistenten ohne Rückfrage (unkritisch, da Änderungen bereits übernommen).

## Offene Fragen
- Aktualisiert `DoAfterMainPlanChanged` auch `PlanLoaded`, sodass `HasData` spätere Schritte korrekt bewertet (z.B. nach Bearbeitung der Koppeltermine)?
- Soll der Assistent in C# auch nachträglich (Menü) aufrufbar sein?
