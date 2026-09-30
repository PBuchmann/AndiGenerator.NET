# DialogPanelAuswaertsKoppelDetail (DialogPanelAuswaertsKoppelDetail.pas + DialogPanelAuswaertsKoppelDetail.dfm)

**Kategorie:** UI-Panel (eingebettet) – Detail-Panel je Mannschaft
**Umfang:** 544 Zeilen .pas / 115 Zeilen .dfm
**Abhängigkeiten (uses):** DialogTeamPanel, DialogPanel, PlanDataObjects (`TDataObject`, `TDataObjectKey`, `THomeDay(List)`, `nRoadCouple`, `aTeamNameA/B`, `aSameDay`, `FormatTimeForExport`, `TimeFromString`), PlanTypes (`MinGameDistance` = 3:30 h), PlanUtils (`MyFormatDate`, `MyFormatTime`, `FixDateTime`, `FixControls`), DialogEditOneAuswaertsKoppel (Unterdialog), System.UITypes; ungenutzt: DialogPanelOptionArray; VCL `TListBox`, `TValueListEditor`, `TPopupMenu`, `MessageDlg`
**Verwendet von:** DialogPanelAuswaertskoppel (`getDialogClass`)

## Zweck
Pflegt für eine Mannschaft (`TeamName`) zwei Dinge:
1. **Auswärtskoppelwünsche** dieser Mannschaft: Paare von Gegnern (A, B), bei denen die Mannschaft beide Auswärtsspiele gekoppelt (am selben Tag und/oder mit Übernachtung) spielen möchte.
2. **Alternative Anfangszeiten** (T2) für eigene Heimspieltermine, die nötig wären, damit **andere** Mannschaften ihre Auswärtskoppelwünsche (die diese Mannschaft betreffen) am gleichen Tag realisieren können.

## Inhalt / Struktur
- UI (.dfm, `PanelMain` `alTop`): `Label1` „Auswärtskoppelwünsche“; `ListBoxTeams` (Einträge „A, B (Typ)“; `OnClick`→EnableButtons, `OnDblClick`→Bearbeiten); Buttons „Neuer Wunsch…“, „Wunsch bearbeiten…“, „Wunsch Löschen…“; `Label2` „Um Auswärtskoppeltermine zu ermöglichen soll bei diesen Terminwünschen im Bedarfsfall eine andere Anfangszeit gewählt werden. (geben Sie die alternative Anfangszeit ein, leer = keine alternative Zeit)“; `ValueListSecondTime: TValueListEditor` (Spalten „Heimspieltermin“ / „alternative Zeit“, editierbar, `OnStringsChange`) mit `PopupMenuAusweichTermine` („Alles setzen“, „Alle löschen“).
- Feld `InLoad` (unterdrückt `OnChange` beim programmatischen Befüllen).
- Methoden:
  - `KoppelNodeToString(Node)` – `teamnamea + ', ' + teamnameb + ' (' + Typ + ')'`, Typ nach `sameday`: 0 „am gleichen Tag“, 1 „mit Übernachtung“, 2 „am gleichen Tag oder mit Übernachtung“. Dieser String dient auch als **Identifikation** des Knotens (`getSelectedNode`).
  - `getMainNode(PlanData)` – Team-Knoten per `TDataObjectKey(team, teamname=TeamName)`.
  - `DataToForm` – ListBox aus `team/roadcouple` (sortiert, Selektion per Index erhalten), dann `DataToAusweichList`, `EnableButtons`.
  - `DataToAusweichList` – für alle eigenen Heimtermine (`GetHomeDays`, `IsHomeDay`) mit `WunschTermineNeedsTimeshiftForAuswaertskoppel = True` eine Zeile `Key = HomeDayToString` (`MyFormatDate(Tag) + ' ' + MyFormatTime(...)`), `Value` = bisherige T2-Zeit oder leer. Keine → Platzhalterzeile „Keine relevanten Termine vorhanden“.
  - `WunschTermineNeedsTimeshiftForAuswaertskoppel(HomeDay)` – siehe Regeln.
  - Buttons: Neu → `TDialogEditOneAuswaertsKoppel.SetValue(PlanData, nil, TeamName)`; bei OK neuer Knoten `roadcouple` unter Team + `GetValue`. Bearbeiten → gleicher Dialog mit selektiertem Knoten. Löschen → `MessageDlg` „Möchten Sie den Auswärtskoppelwunsch … wirklich löschen?“ → `RemoveChild`. Alle drei wirken **sofort** auf `PlanData` und rufen `DataToForm` + `FireOnChange`.
  - `Allesselektieren1Click` („Alles setzen“) – schlägt für alle gelisteten Termine eine Zeit vor (siehe Regeln).
  - `Allesdeselektieren1Click` („Alle löschen“) – leert alle Werte.
  - `FormToData` – liest nur die T2-Zeiten: je gelistetem Heimtermin `TimeFromString(Wert)`; `<> 0` → `KoppelAuswaertsSecondTime := True, KoppelSecondTime := Zeit`, sonst False; `SetHomeDays`. (Die Koppelwünsche selbst sind bereits durch die Buttons in `PlanData`.)
  - `isDefault` – `ChildNodesAreTheSame(eigenerTeamKnoten, DefaultTeamKnoten, 'roadcouple')` **und** keine T2-Zeit eingetragen. Ruft **kein** `FormToData`.
  - `toDefault` – alle `roadcouple` des Teams entfernen, die aus `PlanDataDefault` kopieren, alle T2-Werte leeren, `FormToData`, `DataToForm`, `FireOnChange`.

## Fachliche Logik / Regeln
- **Auswärtskoppelwunsch** (`team/roadcouple[@teamnamea, @teamnameb, @sameday]`): Mannschaft `TeamName` möchte bei A und B gekoppelt auswärts spielen. `sameday`: 0 = am gleichen Tag, 1 = mit Übernachtung (aufeinanderfolgende Tage), 2 = beides zulässig. Kostenart `mktAuswaertsKoppelTermine`.
- **Wann braucht ein eigener Heimtermin eine Alternativzeit?** (`WunschTermineNeedsTimeshiftForAuswaertskoppel`, Z. 89–154): Heimtermin ist kein Heimkoppeltermin, **und** es gibt eine andere Mannschaft X mit Koppelwunsch (A, B), in dem `TeamName` = A oder B vorkommt, `sameday` ∈ {0, 2}, **und** der jeweils andere Partner hat am selben Kalendertag einen Heimtermin, dessen Anfangszeit weniger als `MinGameDistance` (= 3 h 30 min) von der eigenen abweicht. Dann kann X nicht beide Spiele am selben Tag spielen, außer eine der beiden Heimmannschaften bietet eine andere Anfangszeit an.
- **Vorschlag „Alles setzen“** (Z. 262–309): Anfangszeit > 16:59 → Alternativzeit = Zeit − 4 h, sonst Zeit + 4 h.
- Eine Alternativzeit 00:00 ist nicht darstellbar (`TimeFromString` liefert 0 = „keine Zeit“).

## Daten & Persistenz
- `plan/team[@teamname]/roadcouple[@teamnamea, @teamnameb, @sameday]` (Schlüssel `teamnamea+teamnameb`).
- `plan/team/homegameday@coupleauswaertssecondtime="true|false"`, `@couplesecondtime="HH:MM"` (über `SetHomeDays`).
- Persistenz über `*.modifications`-Diff.

## Threading / Performance
Keine. `WunschTermineNeedsTimeshift…` ist O(Heimtermine × Teams × Koppelwünsche × Heimtermine des Partners) mit wiederholtem `GetHomeDays` – bei Ligagrößen unkritisch.

## Plattformabhängigkeiten
VCL `TValueListEditor` (1-basierte `Keys[i+1]` wegen Titelzeile), `MessageDlg`, DPI-Anpassung `MulDiv(DefaultRowHeight, Screen.PixelsPerInch, 96)`.

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels.AwayCoupleTeamViewModel : TeamEditorPageViewModel` mit `ObservableCollection<AwayCoupleWishViewModel>` (Commands Add/Edit/Delete über Dialog-Service) und `ObservableCollection<AlternativeTimeRowViewModel { HomeDay Ref; TimeOnly? AltTime }>`. Die Logik „braucht Alternativzeit“ gehört in einen Core-Service (`AwayCoupleAnalyzer.NeedsAlternativeTime(plan, team, homeDay)`), testbar.
- Mapping: `sameday` 0/1/2 → `enum AwayCoupleMode { SameDay, Overnight, Either }`; Identifikation der Wünsche über Objekt-/Schlüsselreferenz statt Anzeigestring; `TimeOnly?` statt 0 als „leer“.
- Bestätigungsdialog über `IDialogService.ConfirmAsync`.
- **Aufwand:** M – zwei Listen, Unterdialog, fachliche Analyse-Logik.
- **Bugs/Auffälligkeiten:**
  - **Z. 470 / 503–508:** `getMainNode(PlanDataDefault)` bzw. `Node1.FindChild` ohne nil-Prüfung → Zugriffsverletzung, wenn `PlanDataDefault = nil` (selbst erstellter Plan) und `isDefault`/`toDefault` aufgerufen wird (z.B. im SinglePanel-Host, der `isDefault` immer aufruft) bzw. wenn das Team im Default nicht existiert (neu angelegte Mannschaft → `Node2 = nil` ist dort abgefangen, `Node1 = nil` nicht).
  - Z. 84: `MyFormatTime(HomeDay.Date + Trunc(HomeDay.Date))` addiert das Datum doppelt; nur der Zeitanteil wird formatiert → wirkungslos, aber irreführend.
  - Z. 399: `OnlyTime <> 0` → 00:00 nicht als Alternativzeit möglich.
  - Identifikation per Anzeigestring: zwei Wünsche mit gleichem Anzeigetext wären nicht unterscheidbar (durch Schlüssel `teamnamea+teamnameb` praktisch ausgeschlossen, außer bei vertauschten A/B).
  - Eingaben in der Platzhalterzeile „Keine relevanten Termine vorhanden“ werden ignoriert, zählen aber in `isDefault` als Abweichung.
  - Koppelwunsch-Änderungen (Buttons) wirken sofort auf die Arbeitskopie, T2-Zeiten erst bei `FormToData` – uneinheitlich, aber durch den Host-Commit abgedeckt.

## Offene Fragen
- Ist die Heuristik ±4 h für „Alles setzen“ fachlich gewünscht/konfigurierbar?
- Sollen Koppelwünsche mit vertauschten Partnern (A,B) vs. (B,A) als Duplikat erkannt werden?
