# DialogPanelLocationsCompactDetail (DialogPanelLocationsCompactDetail.pas + DialogPanelLocationsCompactDetail.dfm)

**Kategorie:** UI-Panel (eingebettet) – Detail-Panel je Mannschaft
**Umfang:** 455 Zeilen .pas / 148 Zeilen .dfm
**Abhängigkeiten (uses):** DialogTeamPanel, DialogPanel, PlanDataObjects (`TDataObject`, `TDataObjectKey`, `THomeDay(List)`, `TPlanData.getTeamLocationsInt/isValidLocation/FixLocation/GetHomeDays/SetHomeDays`, `nSisterTeam`, `aLocation`, `aGender`, `aTeamName`), PlanUtils (`MyFormatDate`, `GetListComboValue`, `SetListComboValue`, `FixControls`), DialogEditSisterTeam (`TDialogEditSisterTeam`), System.UITypes; VCL `TComboBox`, `TValueListEditor`
**Verwendet von:** DialogPanelLocationsCompact (`getDialogClass`)

## Zweck
Kompakte Pflege der **Spiellokale** (Hallen) einer Mannschaft: Standardspiellokal, abweichende Spiellokale je Heimspieltermin und Spiellokale der **Nachbarmannschaften** (Mannschaften desselben Vereins in anderen Ligen, die dieselben Hallen belegen). Doppelklick auf eine Nachbarmannschaft öffnet deren Detaildialog (Spieltermine der Nachbarmannschaft).

## Inhalt / Struktur
- UI (.dfm; Caption „Spiellokale“):
  - `Panel1` (oben): `LabelKoppelPrio` „Standardspiellokal:“ + `ComboLocation` (DropDownList, `OnChange = ComboLocationChange`), `Label4` „leer, falls Verein nur in einem Spiellokal spielt“, `Label1` „Spiellokale an den konkreten Heimspieltagen:“.
  - `Panel3` (oben) → `ValueListHomeDays` (Spalten „Termin“ / „vom Standardspiellokal abweichendes Spiellokal“, `OnStringsChange = ValueListSisterTeamsStringsChange`).
  - `Panel4` → `Label2` „Spiellokale der Nachbarmannschaften (Doppelklick um einzelne Spieltermine zu bearbeiten):“.
  - `Panel2` (Rest) → `ValueListSisterTeams` (Spalten „Mannschaft“ / „Spiellokal“, `OnStringsChange`, `OnMouseDown`).
- Feld `InLoad`.
- Methoden:
  - `DataToForm` – Combo mit `getTeamLocationsInt` (fest: '', '1'…'5'), Wert `team@location`; `DataToHomeDayList` (je Heimtermin Zeile `MyFormatDate(Datum)` → `HomeDay.Location`, PickList-Editor; leer → Platzhalter „Keine Wunschtermine vorhanden“); Nachbarmannschaften sortiert nach „(Geschlecht) Name“ mit `FixLocation(sisterteam@location)` (leer → Platzhalter „Keine Nachbarmannschaften vorhanden“); `FillCombos` (PickLists mit Lokalliste befüllen).
  - `FormToData` – `team@location := Combo`; `HomeDayListToData` (je Heimtermin Wert prüfen: ungültig → '' und Zelle leeren; `HomeDay.Location := Wert`; `SetHomeDays`); je Nachbarmannschaft analog `sisterteam@location`.
  - `ValueListSisterTeamsStringsChange` (für **beide** Listen) – wenn nicht `InLoad`: `FormToData`, `FireOnChange`, `FillCombos` (Sofort-Übernahme).
  - `ComboLocationChange` – `FormToData`, `FillCombos` (**ohne** `FireOnChange`).
  - `ValueListSisterTeamsMouseDown` – Zeile selektieren; Doppelklick links in Spalte 0 → `TeamsDblClick`: `TDialogEditSisterTeam.SetValues(SisterNode, PlanData, TeamName)`; bei OK `GetValues(SisterNode)`, `DataToForm`, `FireOnChange`.
  - `isDefault` – Standardlokal leer, alle Nachbarlokale leer, alle Heimtermin-Lokale leer oder gleich dem Standardlokal (**kein** Vergleich mit `PlanDataDefault`).
  - `toDefault` – alle Werte leeren, `FormToData`, `DataToForm`, `FireOnChange`.
  - `getSelectedSisterNode`, `FindSisterNodeByKey` – Identifikation über Anzeigestring „(gender) teamname“.
  - `EnableButtons` – leer.

## Fachliche Logik / Regeln
- **Gültige Spiellokale:** nur '' (Standard/keins) und '1'…'5' (`getTeamLocationsInt`; Kommentar im Code: „Click-tt kann nur numerische Spiellokale“). Ungültige Eingaben werden auf '' zurückgesetzt.
- **Leeres Standardspiellokal** = Verein spielt nur in einem Spiellokal.
- Heimtermin-Lokal leer = Standardlokal der Mannschaft.
- Nachbarmannschaften (`sisterteam`) dienen der Hallenbelegungsprüfung (Kostenarten `mktHalleBelegt` „Hallenbelegung“, `mktSisterGames` „parallele Spiele“): Spiele verschiedener Vereinsmannschaften im selben Lokal zur selben Zeit kollidieren.

## Daten & Persistenz
- `plan/team@location`
- `plan/team/homegameday@location` (über `SetHomeDays`)
- `plan/team/sisterteam[@teamname, @gender]@location` (Schlüssel `teamname+gender`); Unterdialog bearbeitet zusätzlich `sisterteam/sistergame`.
- Persistenz über `*.modifications`-Diff.

## Threading / Performance
Keine. Jede Zelländerung führt `FormToData` (inkl. `GetHomeDays`/`SetHomeDays` mit Neuaufbau aller Heimtermin-Knoten) und `FillCombos` aus – unkritisch.

## Plattformabhängigkeiten
VCL `TValueListEditor` (PickList-Editor, 1-basierte Keys, `FindRow`, `MouseToCell`), Doppelklick-Erkennung über `ssDouble in Shift` im `OnMouseDown`, DPI-Anpassung der Zeilenhöhe.

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels.LocationsTeamViewModel : TeamEditorPageViewModel` mit `string DefaultLocation`, `ObservableCollection<HomeDayLocationRow>`, `ObservableCollection<SisterTeamLocationRow>` + `ICommand EditSisterTeam` (öffnet Sister-Team-Dialog über Dialog-Service).
- Lokale als `enum`/`int?` (1–5) oder validierter String; Liste der gültigen Lokale zentral im Core (`Plan.ValidLocations`), da Click-TT-abhängig.
- Heimtermin-Zeilen über Objekt/`DateTime` identifizieren statt über formatierten Datumstext.
- Änderungsereignisse (inkl. Standardlokal) konsistent auslösen.
- **Aufwand:** M – drei gekoppelte Listen plus Unterdialog.
- **Bugs/Auffälligkeiten:**
  - Z. 195/225/288: Schlüssel der Heimtermin-Zeilen ist nur das Datum (`'ddd dd.mm.yyyy'`); zwei Heimtermine am selben Tag erzeugen doppelte Keys → `Values[Key]` liefert die erste Zeile, der zweite Termin erhält deren Lokal.
  - Z. 93–97: Änderung des Standardspiellokals ohne `FireOnChange`.
  - Z. 268/293: Schreiben in `ValueList.Values` innerhalb des `OnStringsChange`-Handlers löst den Handler rekursiv erneut aus (terminiert, da Wert danach gültig).
  - `isDefault` vergleicht mit festen Leerwerten statt mit den Click-TT-Originaldaten (anderes Modell als z.B. FreeDays/60km).
  - Eingaben in Platzhalterzeilen werden ignoriert.

## Offene Fragen
- Bleibt die Beschränkung auf Lokale 1–5 (Click-TT-Vorgabe) bestehen?
- Soll „Standard wiederherstellen“ auf die Click-TT-Werte statt auf „alles leer“ zurücksetzen?
