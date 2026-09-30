# DialogPanelHomeCoupleCompactDetail (DialogPanelHomeCoupleCompactDetail.pas + DialogPanelHomeCoupleCompactDetail.dfm)

**Kategorie:** UI-Panel (eingebettet) – Detail-Panel je Mannschaft
**Umfang:** 665 Zeilen .pas / 48 Zeilen .dfm
**Abhängigkeiten (uses):** DialogTeamPanel, DialogPanel, PlanDataObjects (`THomeDay(List)`, `GetHomeDays/SetHomeDays`), PlanTypes (`TWunschterminOption`, `cWunschterminOptionName`), PlanUtils (`MyFormatDate`, `FixControls`), System.UITypes; VCL `TValueListEditor` (PickList-Zellen)
**Verwendet von:** DialogPanelHomeCoupleCompact (`getDialogClass`)

## Zweck
Kompakte Bearbeitung der **Heimkoppeltermine** (zwei Heimspiele am selben Tag) und **Doppelspieltage** (Heimspiele an zwei aufeinanderfolgenden Tagen) einer Mannschaft inkl. Priorität (möglich / gewünscht / hohe Prio). Es werden nur Konstellationen angeboten, die sich aus den vorhandenen Wunschterminen ergeben; neue Termine oder zweite Anfangszeiten können hier nicht angelegt werden (Hinweis im Header des Wrappers).

## Inhalt / Struktur
- Hilfsklassen:
  - `TKoppelWunschElem` – `First`, `Second: THomeDay` (besitzende Kopien; `Second = nil` bei Einzeltag).
  - `TCalculateOptionsKoppelDate` – `Date1`, `Date2` (0 = Einzeltag), `Option1`, `Option2: TWunschterminOption`; `isTheSame`.
- `TDialogPanelHomeCoupleCompactDetail = class(TDialogTeamPanel)`
  - UI (.dfm): Caption „Heimkoppel/Doppelspieltage“; `PanelMain` (BorderWidth 16) → `ValueListHomeKoppel` (Spalten „Termin“ / „Einstellung“, `OnStringsChange`).
  - Felder: `InLoad`, `workHomeKoppelList: TObjectList<TCalculateOptionsKoppelDate>`, `mapRowToHomeKoppel: TDictionary<Zeile, Elem>`.
- Methoden:
  - `getKoppelWunschListForMannschaft` – aus den Heimterminen: für jeden Termin mit Heimtermin am **Folgetag** ein Paar (Tag, Folgetag); für Termine **ohne** Nachbarn, die bereits `KoppelDate` sind, ein Einzeleintrag.
  - `LoadKoppelDates(PlanData, List)` – wandelt in `TCalculateOptionsKoppelDate` mit `KoppelValueFromOptions(THomeDay)` (lokale Funktion: Koppel/Doppel × Prio 0/1/2 → `woKoppelPossible/Soft/Hard` bzw. `woKoppelDoubleDatePossible/Soft/Hard`, sonst `woKoppelNothing`).
  - `DataObjectToForm(PlanData)` / `DataToForm` – je Element eine Zeile; Einzeltag: Key `MyFormatDate(Date1)`, PickList {nichts, möglich, gewünscht, hohe Prio}; Paar: Key „<Datum1> und <Datum2>“, PickList mit **13 Einträgen** (`getComboElemsForHeimKoppelWuensche`), Vorauswahl über `getComboIndexFromOptions`. Zeilen sind `ReadOnly` (nur Auswahl aus PickList). Leer → Platzhalter „Koppelspiele sind in den Terminwünschen nicht enthalten“.
  - `getHomeKoppelOptionsByIndexAndTwoDates(Index)` – Index → (Option1, Option2), siehe Tabelle; `getComboIndexFromOptions` = Rückwärtssuche (Default 0).
  - `setHomeKoppelOptionsByIndex(Elem, Index)` – Einzeltag: 0..3; Paar: Tabelle.
  - `FormToData` – Picklist-Auswahl je Zeile → Elemente; dann **alle** Heimtermine der Mannschaft `KoppelDate := False, DoubleDate := False, KoppelPrio := 0`; danach je Element Flags/Prio auf Date1 bzw. Date2 setzen; `SetHomeDays`.
  - `isDefault` – **`FormToData()`** (Seiteneffekt), `LoadKoppelDates(PlanDataDefault)` und positionsweiser Vergleich mit `workHomeKoppelList`.
  - `toDefault` – `DataObjectToForm(PlanDataDefault)`, `FormToData`, `DataToForm`, `FireOnChange`.

## Fachliche Logik / Regeln
**Auswahlliste für ein Tagespaar (Index → Option1 / Option2):**
| Idx | Anzeige | Tag 1 | Tag 2 |
|---|---|---|---|
| 0 | kein Koppeltermin/Doppelspieltag | – | – |
| 1 | Doppelspieltag möglich | D0 | D0 |
| 2 | Doppelspieltag gewünscht | D1 | D1 |
| 3 | Doppelspieltag gewünscht (hohe Prio) | D2 | D2 |
| 4–6 | „<Datum1>: Zwei Heimspiele an diesem Tag möglich/gewünscht/(hohe Prio)“ | K0/K1/K2 | – |
| 7–9 | „<Datum2>: …“ | – | K0/K1/K2 |
| 10–12 | „<Datum1> und <Datum2>: …“ | K0/K1/K2 | K0/K1/K2 |
- Prioritäten: 0 = möglich, 1 = gewünscht, 2 = gewünscht (hohe Prio) → `KoppelPrio`. Doppelspieltag = `doublegameday`, Heimkoppel = `couplegameday`. Kostenart `mktKoppelTermine` („Heimkoppel“).
- Einzeltage ohne Nachbartermin erscheinen nur, wenn sie bereits Koppeltermin sind (neue nur im Wunschtermin-Panel, dort mit zweiter Anfangszeit `K<p>:HH:MM`).

## Daten & Persistenz
Über `GetHomeDays/SetHomeDays`: `plan/team/homegameday@couplegameday`, `@doublegameday`, `@coupleprio`, `@couplesecondtime`. Persistenz über `*.modifications`-Diff.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL `TValueListEditor` mit `TItemProp` (`esPickList`, `ReadOnly`), `ItemProps[Row-1]` (0-basiert) vs. `Cells[1,Row]` (1-basiert inkl. Titelzeile), DPI-Anpassung der Zeilenhöhe.

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels.HomeCoupleTeamViewModel : TeamEditorPageViewModel` mit `ObservableCollection<CoupleRowViewModel { string Label; IReadOnlyList<CoupleChoice> Choices; CoupleChoice Selected; }>`; View: `DataGrid` mit `ComboBox`-Spalte.
- Die „grausame Case-Latte“ als deklarative Tabelle `static readonly (CoupleOption d1, CoupleOption d2)[] PairChoices` im Core; Einzeltag-Liste separat. Mapping HomeDay↔Option als reine Funktionen (testbar).
- `TWunschterminOption` → `enum CoupleOption`; Anzeigenamen als Ressourcen.
- `isDefault` ohne `FormToData`-Seiteneffekt; Vergleich null-sicher.
- **Aufwand:** M – Logik kompakt, aber viele Sonderfälle (Paar/Einzeltag, Dreier-Ketten) und Kompatibilität der Flags.
- **Bugs/Auffälligkeiten:**
  - **Z. 243–259 + 481–483:** Kombinationen, die nicht in der 13er-Tabelle vorkommen (z.B. Tag 1 = Doppelspieltag, Tag 2 = Koppel; oder unterschiedliche Prioritäten D1/D2; oder Doppelspieltag nur an einem Tag) werden als Index 0 „kein Koppeltermin“ angezeigt und beim nächsten `FormToData` **gelöscht**. Da `isDefault` (→ `FormToData`) schon beim Öffnen des Panels vom Host aufgerufen wird, genügt **das bloße Anzeigen**, um solche Daten in der Arbeitskopie zu verlieren (wirksam bei OK).
  - Z. 139–170: Drei aufeinanderfolgende Heimtermine (Fr/Sa/So) erzeugen zwei überlappende Paare (Fr+Sa, Sa+So); der mittlere Tag wird von beiden Zeilen beschrieben – die spätere Zeile gewinnt teilweise (Flags werden nur gesetzt, nie zurückgesetzt → Mischzustände möglich).
  - Wird per Paar-Auswahl ein **neuer** Heimkoppeltermin (Idx 4–12) gesetzt, bleibt `KoppelSecondTime = 0` → gespeichert als `couplesecondtime="00:00"` (keine sinnvolle zweite Anfangszeit).
  - Z. 571: `LoadKoppelDates(PlanDataDefault, …)` ohne nil-Prüfung → Zugriffsverletzung bei `PlanDataDefault = nil`, falls `isDefault` aufgerufen wird (SinglePanel-Host ruft immer `isDefault`).
  - Z. 306–313: lokale Variable `S` wird berechnet, aber nie verwendet.

## Offene Fragen
- Wie sollen nicht darstellbare Kombinationen (gemischte Koppel/Doppel, ungleiche Prioritäten) behandelt werden – erhalten, anzeigen als „benutzerdefiniert“ oder normalisieren?
- Welche zweite Anfangszeit soll bei neu gesetzten Heimkoppelterminen gelten (Pflichteingabe? Vorschlag wie ±4 h)?
- Umgang mit Ketten von ≥3 aufeinanderfolgenden Heimterminen.
