# DialogCreateTestData (DialogCreateTestData.pas + DialogCreateTestData.dfm)

**Kategorie:** UI-Dialog (Entwickler-Werkzeug, faktisch toter Code)
**Umfang:** 249 Zeilen .pas / 122 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `PlanTypes`, `PlanUtils`; RTL/VCL: `Vcl.Forms/StdCtrls/ExtCtrls/ComCtrls/Menus`, `Dialogs`, `System.Generics.Collections/Defaults`, `System.IOUtils`, `System.TypInfo`, `SyncObjs`, `Winapi.Windows/Messages/ShellAPI`
**Verwendet von:** niemandem. Weder `AndiGenerator.dpr` noch irgendeine andere Unit referenziert `DialogCreateTestData`, `TFormTestData` oder `ShowDialog` (grep).

## Zweck
Entwickler-Hilfsdialog zum Erzeugen synthetischer Testdaten: Aus einem geladenen `TPlan` sollte eine Kopie mit zufällig gewürfelten Wunsch- und Sperrterminen je Mannschaft erzeugt und als XML gespeichert werden. Die eigentliche Erzeugungslogik in `ButtonSaveClick` ist **vollständig auskommentiert** (Kommentar-Marker `ARBEIT`), der Dialog tut aktuell nichts außer sich zu öffnen und zu schließen.

## Inhalt / Struktur
- `TFormTestData = class(TForm)`, öffentliches Feld `Plan: TPlan`.
- `procedure ShowDialog(Plan: TPlan)` – erzeugt Dialog mit `Owner=nil`, setzt `Plan`, `ShowModal`, `Free`.
- `GetSpieltagDiff: integer` – wählt zufällig eine Zeile aus `Memo1` (Wochentagskürzel `MO..SO`) und liefert Offset 0..6 zum Montag. Unbekannte Kürzel → 0.
- `GetNumSpieltage` – `a + Random(b-a)` mit a=`Edit1`, b=`Edit2` (Anzahl Wunschtermine pro Halbserie).
- `GetNumSperrTermine` – `a + Random(b-a)` mit a=`EditSperrVon`, b=`EditSperrBis`.
- `ButtonSaveClick` – leer (Code auskommentiert, s.u.).
- `ButtonOKClick` → `ModalResult := mrOK` („Schließen").
- `FormCreate` → `FixControls(Self)` (automatische Tab-Reihenfolge, Wine-Font-Fix).

UI (.dfm, Caption „Testdaten...", `bsSizeToolWin`, 275×393):
- `Memo1` (Default-Zeilen `MO`, `FR`, `SA`) – erlaubte Spieltags-Wochentage.
- `Edit1`/`Edit2` („Anzahl", Default 7/7) – Bereich Wunschtermine je Runde.
- `EditSperrVon`/`EditSperrBis` („Spertermine" [sic], Default 0/0).
- Buttons „Speichern", „Schließen"; `SaveDialog` (Filter `*.xml`, aber `DefaultExt='csv'` – inkonsistent).

## Fachliche Logik / Regeln
Nur im auskommentierten Code (zur Dokumentation, falls die Funktion reaktiviert werden soll):
- Plan kopieren, `clearAllDates`, Montage der Spielwochen für Vorrunde (`DateBegin..DateBeginRueckrundeInXML`), Rückrunde (`..DateEnd`) und gesamt ermitteln (`getSpieltagMondays`).
- Je Mannschaft: WunschTermine, SperrTermine, AuswaertsKoppelInXML leeren; pro Runde (j=0 Vorrunde, j=1 Rückrunde) bis zu 201 Versuche, bis `NumSpielTage` eindeutige Wunschtermine „Montag + Wochentagsoffset + 15:00 Uhr" erzeugt sind.
- Sperrtermine: zufälliger Montag + zufälliger Wochentagsoffset (neuer `GetSpieltagDiff()`-Aufruf!), nur wenn am Tag kein Wunschtermin existiert und noch nicht vorhanden.
- Speichern via `SavePlan.SaveToXML`.
- Der auskommentierte Code referenziert alte API (`TPlan.Mannschaften[].WunschTermine`, `TWunschTermin`) – vermutlich nicht mehr kompilierbar.

## Daten & Persistenz
Keine aktive. (Geplant: XML via `TPlan.SaveToXML`.)

## Threading / Performance
Keine. Nutzt globales `Random`.

## Plattformabhängigkeiten
Nur VCL-Formular/`TSaveDialog`.

## Migrationshinweise für C#
- Empfehlung: **nicht migrieren** (toter Code). Falls Testdatengenerator gewünscht: als Konsolen-/Test-Hilfsklasse in `AndiGenerator.Tests` (z.B. `TestDataGenerator`) mit `System.Random(seed)` für Reproduzierbarkeit.
- Fallstrick: `Random(0)` liefert in Delphi 0; in C# `rng.Next(0)` ebenfalls 0, aber `Next(a, b)` mit a==b ist ok, mit a>b wirft es → validieren.
- `StrToInt` wirft bei Nicht-Zahlen – im Original keine Validierung.
- Aufwandsschätzung: **S** (bzw. 0, wenn verworfen).
- Auffälligkeiten: `ButtonSaveClick` leer (Z. 139–242); `SaveDialog.DefaultExt='csv'` vs. Filter `*.xml`; Tippfehler „Spertermine".

## Offene Fragen
- Soll der Testdatengenerator überhaupt in die C#-Version übernommen werden (z.B. für Benchmarks des Optimierers)?
