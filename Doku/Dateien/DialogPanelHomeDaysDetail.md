# DialogPanelHomeDaysDetail (DialogPanelHomeDaysDetail.pas + DialogPanelHomeDaysDetail.dfm)

**Kategorie:** UI-Panel (eingebettet) – Detail-Panel je Mannschaft
**Umfang:** 521 Zeilen .pas / 87 Zeilen .dfm
**Abhängigkeiten (uses):** DialogTeamPanel, DialogPanel, PlanDataObjects (`THomeDay`, `THomeDayList`, `TPlanData.GetHomeDays/SetHomeDays`, `aFrom`, `aUntil`), PlanUtils (`MondayBefore`, `MyFormatDate`, `FixControls`), PlanTypes, Math (`Min/Max`); implementation: DialogEditOneHomeDay (`TDialogEditOneHomeDay`); ungenutzt: DialogPanelOptionArray, DialogForSingleGewichtungsOptions; VCL `TStringGrid`, `TInplaceEdit`, `TPopupMenu`, WinAPI `GetCursorPos`
**Verwendet von:** DialogPanelHomeDays (`getDialogClass`); nur in uses-Liste: DialogEditOneHomeDay (zirkulär), DialogFirstStart

## Zweck
Wochenkalender-Editor für die **Wunschtermine (Heimspieltermine) und Sperrtermine** einer Mannschaft. Jede Zeile ist eine Kalenderwoche (Mo–So), jede Zelle ein Tag; der Zellinhalt ist eine **Kurzsyntax** (`THomeDay.AsShortString/FromShortString`), die Uhrzeit, Sperrtermin, Ausweichtermin, Koppel-/Doppelspieltag-Optionen, zweite Anfangszeit, max. parallele Spiele und Spiellokal kodiert. Enthält die einzige echte Validierung (`CheckData`) unter den hier dokumentierten Panels.

## Inhalt / Struktur
- UI (.dfm): Caption „Wunschtermine“; `PanelMain` → `PanelBottom` (BorderWidth 16) → `Grid: TStringGrid` (8 Spalten, Optionen `goEditing, goAlwaysShowEditor, goRangeSelect …`, Events `OnGetEditText`, `OnDblClick`, `OnContextPopup`); unsichtbare Labels `LabelAbstand` (Breite 120 = Spalte 0) und `LabelColSize` (Breite 45 = Mindestbreite Tagesspalten) als DPI-skalierte Maßvorlagen; `PopupMenu` mit „Bearbeiten…“ und „Löschen“.
- Felder: `DefaultValues: THomeDayList` (**unbenutzt**), `OldSelCol/OldSelRow` (zuletzt editierte Zelle).
- Grid-Aufbau (`HomeDaysToForm`): Kopfzeile „Mo…So“; Startdatum `MondayBefore(plan@from)`, Ende `Min(Start + 360, plan@until)`; je Woche Zeile mit Spalte 0 = `'dd.mm - dd.mm.yyyy'`, Spalten 1–7 = `GetTeamDateString(Tag)` (letzter passender HomeDay des Tages → `AsShortString`). Hängt das PopupMenu per Hack-Klasse `THackInplaceEdit` an den Inplace-Editor. `AutoSizeGrid` passt Spaltenbreiten an den Text an (`Canvas.TextWidth + 20`).
- `FormToHomeDays(List)` – iteriert dieselben Wochen/Tage, parst jede Zelle mit `THomeDay.FromShortString(Tag, Text)`; nur erfolgreich geparste (Uhrzeit oder FREI) werden übernommen.
- `DataToForm` = `PlanData.GetHomeDays(TeamName)` → `HomeDaysToForm`. `FormToData` = `FormToHomeDays` → `PlanData.SetHomeDays(TeamName, List)`.
- `CheckData` – Doppelspieltag-Prüfung (siehe Regeln), bei Fehler `ShowMessage` und False (blockiert Mannschafts-/Panelwechsel und OK im Multi-Host).
- `isDefault` – `PlanDataDefault.GetHomeDays` vs. (nach **`FormToData()`**-Seiteneffekt) `PlanData.GetHomeDays`; gleiche Anzahl und jedes Default-Element per `THomeDay.isTheSame` gefunden.
- `toDefault` – Default-HomeDays ins Grid.
- Bedienung:
  - Direkte Texteingabe in die Zelle. `GridGetEditText` (feuert beim Betreten einer Zelle) normalisiert die **vorher** editierte Zelle über `FixCellStringValue` (Parse+Format; ungültig → leer), normalisiert den aktuellen Wert, `FireOnChange`, `AutoSizeGrid`.
  - Doppelklick / Kontextmenü „Bearbeiten…“ → `TDialogEditOneHomeDay` (modal) für die Zelle; Ergebnis als Kurzstring zurück in die Zelle.
  - „Löschen“ → Zelle leeren (nur aktiv, wenn nicht leer).

## Fachliche Logik / Regeln
**Kurzsyntax einer Tageszelle** (Tokens durch Leerzeichen getrennt; Anführungszeichen schützen Leerzeichen im Lokalnamen; Groß-/Kleinschreibung egal):
| Token | Bedeutung | THomeDay-Feld |
|---|---|---|
| `HH:MM` (Pos. 3 = `:`) | Heimspiel-Anfangszeit (Pflicht für Wunschtermin) | `Date = Tag + Zeit` |
| `FREI` | Sperrtermin – an dem Tag kein Spiel; alle anderen Tokens werden verworfen | `SperrTermin` |
| `A` | Ausweichtermin | `AusweichTermin` |
| `K<p>:HH:MM` (p=0..2) | Heimkoppel (2 Heimspiele an diesem Tag), Priorität 0=möglich, 1=gewünscht, 2=hohe Prio, zweite Anfangszeit | `KoppelDate, KoppelPrio, KoppelSecondTime` |
| `D<p>` (p=0..2) | Doppelspieltag (zusammen mit Nachbartag), Priorität | `DoubleDate, KoppelPrio` |
| `T2:HH:MM` | alternative Anfangszeit für Auswärtskoppeltermine (nicht bei K) | `KoppelAuswaertsSecondTime, KoppelSecondTime` |
| `Max:<n>` | maximale Anzahl paralleler Heimspiele | `MaxParallelGame` |
| `L:<Lokal>` | abweichendes Spiellokal (gültig: '' oder '1'…'5') | `Location` |
| leer | kein Heimspiel, Auswärtsspiel möglich | `NoDate` |

- **Validierung Doppelspieltag** (`CheckData`, Z. 72–111): Für jeden Tag mit `DoubleDate` muss ein Termin am Vor- oder Folgetag existieren (`|Tag1 − Tag2| = 1`), sonst Meldung „Am … wurde ein Doppelspieltag angegeben. Das ist nur zulässig, wenn am darauf folgenden Tag auch ein Doppelspieltag ist …“.
- **Sperrtermine vs. Wunschtermine:** `SetHomeDays` schreibt `FREI` als `team/nogameday@date`, alle anderen als `team/homegameday`. `GetHomeDays` verwirft Wunschtermine an Sperrtagen.
- Zeitraum: nur die ersten ~52 Wochen (360 Tage ab Montag vor Saisonbeginn) sind editierbar.
- Genau **eine Zelle pro Tag** → maximal ein Wunschtermin pro Tag und Mannschaft.

## Daten & Persistenz
`plan/team[@teamname]/homegameday[@datetime, parallelgames, ausweichtermin, location, doublegameday, couplegameday, coupleauswaertssecondtime, couplesecondtime, coupleprio]` und `plan/team/nogameday[@date]`. Zeitraum aus `plan@from`/`plan@until`. Persistenz über `*.modifications`-Diff; da `SetHomeDays` alle Knoten löscht und neu erzeugt, erscheinen im Diff alle Attribute explizit.

## Threading / Performance
Keine. `AutoSizeGrid` misst bei jeder Zellbetretung alle ~364 Zellen (unkritisch).

## Plattformabhängigkeiten
- `TStringGrid` mit Inplace-Editor und **Hack-Klasse** `THackInplaceEdit` (Zugriff auf protected `PopupMenu`), `OnGetEditText` als Änderungs-Hook (VCL-Spezifikum).
- WinAPI `GetCursorPos` + `ScreenToClient` im Doppelklick-Handler.
- `Canvas.TextWidth` für Spaltenbreiten; unsichtbare Labels als DPI-Maßvorlagen.
- `DateTimeToString('dd.mm')` / `MyFormatDate` (locale-abhängige Wochentage).

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels.HomeDaysTeamViewModel : TeamEditorPageViewModel` mit `ObservableCollection<WeekRowViewModel>` (7 × `DayCellViewModel { DateOnly Date; string Text; bool IsValid }`), View als `DataGrid`/`ItemsRepeater`-Kalender (Avalonia). Kurzsyntax-Parser/Formatter in `AndiGenerator.Core` (`HomeDayShortSyntax.TryParse/Format`) mit Unit-Tests (1:1 zu `THomeDay.FromShortString/AsShortString`).
- Mapping: `THomeDay` → `record HomeDay(DateTime Start, bool IsBlocked, bool IsAlternative, CoupleKind Couple, int CouplePrio, TimeOnly? SecondTime, int MaxParallel, string Location)`; `TDateTime` → `DateTime`/`DateOnly`+`TimeOnly`; `MondayBefore` → `date.AddDays(-(((int)date.DayOfWeek + 6) % 7))`.
- Validierung als `INotifyDataErrorInfo` am ViewModel (Meldung inline statt `ShowMessage`), sofortige Zell-Validierung statt Normalisierung beim Verlassen.
- Änderungserkennung bei jeder Texteingabe (Binding), nicht erst beim Betreten der nächsten Zelle.
- **Fallstricke:** `TimeFromString` erwartet exakt `HH:MM` (Position 3 = `:`), `9:00` wird **nicht** erkannt; `FixDateTime` normalisiert Gleitkomma-Rundung von `TDateTime`; `SplitString` verarbeitet das letzte Zeichen speziell (Parser exakt nachbauen oder bewusst robuster machen).
- **Aufwand:** M – Kalender-Grid-UI plus exakt kompatibler Kurzsyntax-Parser.
- **Bugs/Auffälligkeiten:**
  - **Z. 92:** `if HomeDay.DoubleDate then` statt `HomeDay2.DoubleDate` – die Prüfung akzeptiert als Partner **jeden** Termin am Nachbartag, nicht nur einen Doppelspieltag. Die Fehlermeldung verspricht mehr, als geprüft wird.
  - Z. 467: `if Item1 <> Item2` vergleicht Objektreferenzen aus verschiedenen Listen – immer wahr, wirkungslos.
  - Z. 341–342 / 413–414: Termine nach Start+360 Tagen werden nicht angezeigt und beim `FormToData` (via `SetHomeDays`, das alle Knoten löscht) **verloren**.
  - Mehrere Wunschtermine am selben Tag (möglich im Click-TT-Import) werden auf einen reduziert (`GetTeamDateString` nimmt den letzten).
  - `FixCellStringValue` parst mit `Trunc(Now)` als Datum – nur Formatierung, fachlich folgenlos.
  - `ListViewTeamsClick` (Z. 487) ist ein toter Handler; `DefaultValues` unbenutzt.
  - Zirkuläre Unit-Referenz mit DialogEditOneHomeDay (dort nur in uses, ungenutzt).

## Offene Fragen
- Soll der Doppelspieltag-Check (Z. 92) korrigiert werden (Partner muss ebenfalls `D` sein)? Dann können bestehende Pläne beim Speichern plötzlich abgelehnt werden.
- Sollen mehrere Termine pro Tag unterstützt werden?
- Zeitraum > 360 Tage (z.B. Doppelrunden über Jahreswechsel hinaus) – Begrenzung beibehalten?
