# DialogPanelHomeRightDetail (DialogPanelHomeRightDetail.pas + DialogPanelHomeRightDetail.dfm)

**Kategorie:** UI-Panel (eingebettet) – Detail-Panel je Mannschaft
**Umfang:** 306 Zeilen .pas / 93 Zeilen .dfm
**Abhängigkeiten (uses):** DialogTeamPanel, DialogPanel, PlanDataObjects (`TDataObject`, `TDataObjectKey`, `nHomeRights`, `aHomeRights`, `aHomeRight`, `aTeamName`), PlanTypes, PlanUtils (`FixControls`), System.UITypes; ungenutzt: DialogEditSisterTeam; VCL `TComboBox`, `TValueListEditor`
**Verwendet von:** DialogPanelHomeRight (`getDialogClass`)

## Zweck
Festlegung des **Heimrechts** einer Mannschaft: (1) wie viele ihrer Heimspiele in der Vorrunde (und damit in der Rückrunde) liegen sollen, (2) je Gegner, ob die Mannschaft in der Vorrunde oder in der Rückrunde Heimrecht hat oder ob es egal ist.

## Inhalt / Struktur
- UI (.dfm; Caption der Form irrtümlich „Spiellokale“): `Panel1` mit `LabelKoppelPrio` „Anzahl Heimspiele:“ + `ComboNumberHome` (DropDownList, `OnChange = ComboNumberHomeChange`), `Label1` „Heimrecht bei diesen Mannschaften:“; `Panel3` → `ValueListHomeRights` (Spalten „Gegenerische Mannschaft“ / „Heimrecht“, `OnStringsChange = ValueListSisterTeamsStringsChange`).
- Feld `InLoad`.
- Methoden:
  - `FillComboNumberHome(n)` – Eintrag 0 „Automatisch“, danach für i = 0..n−1: „Vorrunde: i      Rückrunde: n−i−1“ (n = Anzahl Mannschaften; jede Mannschaft hat n−1 Heimspiele gesamt).
  - `DataToForm` – Combo füllen, `ItemIndex := team@homerights + 1`; `DataToHomeDayList` (Name irreführend): je anderem Team eine Zeile mit Wert „Vorrunde“ / „egal“ / „Rückrunde“ aus `team/homerights[@teamname,@homeright]` (1 = Vorrunde, 2 = Rückrunde); PickList {Vorrunde, egal, Rückrunde}, `ReadOnly`.
  - `FormToData` – `team@homerights := ItemIndex − 1` (−1 = automatisch); `HomeDayListToData`: `deleteChilds(nHomeRights)`, für jede Zeile ≠ „egal“ neuer Knoten `homerights teamname="…" homeright="1|2"`.
  - `ValueListSisterTeamsStringsChange` – wenn nicht `InLoad`: `FormToData` + `FireOnChange` (Sofort-Übernahme).
  - `ComboNumberHomeChange` – nur `FormToData` (**kein** `FireOnChange`).
  - `isDefault` – True, wenn Combo = „Automatisch“ und alle Zeilen „egal“ (**kein** Vergleich mit `PlanDataDefault`).
  - `toDefault` – Combo 0, alle Zeilen „egal“, `FormToData`, `DataToForm`, `FireOnChange`.
  - `EnableButtons` – leer.

## Fachliche Logik / Regeln
- `team@homerights`: −1 = automatisch, sonst gewünschte Anzahl Heimspiele in der Vorrunde (Engine: `TMannschaft.HomeRightCountRoundOne`); die Rückrunde ergibt sich als (n−1) − Wert.
- `homerights@homeright` je Gegner: 1 = Heimrecht in der Vorrunde (`hrRound1`), 2 = in der Rückrunde (`hrRound2`); fehlend/0 = egal (`hrNone`) – Engine `TMannschaft.loadHomeRights`.
- Die Angabe wird **einseitig** beim bearbeiteten Team gespeichert; eine Konsistenzprüfung mit der Gegenseite (Gegner beansprucht ebenfalls Vorrunde) gibt es im Panel nicht.
- Kostenarten mit Bezug: `mktZahlHeimSpielTermine` („Ungleich H/A“), `mktWechselHeimAuswaerts`, `mktAbstandHeimAuswaerts` – ob das Heimrecht als harte Vorgabe oder über Kosten wirkt, ist in PlanTypes zu klären.

## Daten & Persistenz
`plan/team@homerights` (int) und `plan/team/homerights[@teamname, @homeright]` (Schlüssel `teamname`). Click-TT-Import setzt `homerights = -1` (PlanDataObjects Z. 1654). Persistenz über `*.modifications`-Diff.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
VCL `TValueListEditor` (PickList, 1-basierte Keys), `TComboBox`; DPI-Anpassung der Zeilenhöhe.

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels.HomeRightTeamViewModel : TeamEditorPageViewModel` mit `int? HomeGamesFirstRound` (null = automatisch; Auswahl als Liste von `HomeCountChoice`) und `ObservableCollection<OpponentHomeRightViewModel { string Opponent; HomeRight Value }>`; `enum HomeRight { None, FirstRound, SecondRound }`.
- Core: `Team.HomeGamesFirstRound : int?`, `Team.HomeRights : Dictionary<string, HomeRight>`.
- Optionale Konsistenzprüfung (Gegner hat widersprüchliche Angabe) als Validierungshinweis ergänzen (Offene Frage).
- **Aufwand:** S.
- **Bugs/Auffälligkeiten:**
  - **Z. 71:** `GetAsInt(aHomeRights)` liefert bei fehlendem Attribut 0 → Combo zeigt „Vorrunde: 0 Rückrunde: n−1“ statt „Automatisch“; beim nächsten `FormToData` wird 0 gespeichert. Betrifft Pläne, deren Team-Knoten ohne Click-TT-Import entstanden sind (Import setzt −1). Die Engine interpretiert den fehlenden Wert ebenfalls als 0.
  - Z. 53–56: Combo-Änderung ohne `FireOnChange` → „Standard wiederherstellen“-Button wird erst verzögert aktualisiert.
  - Z. 104: `getMainNode(PlanData).NamedChilds` ohne nil-Prüfung (Team existiert i.d.R.).
  - `isDefault` vergleicht nicht mit den Click-TT-Originaldaten, sondern mit festen Standardwerten (anderes Semantikmodell als bei anderen Panels).
  - Form-Caption „Spiellokale“ und Methodennamen `DataToHomeDayList`/`HomeDayListToData`/`ValueListSisterTeamsStringsChange` sind Copy-&-Paste-Reste.

## Offene Fragen
- Soll eine Konsistenzprüfung des Heimrechts zwischen beiden Mannschaften erfolgen?
- Soll `homerights` fehlend = automatisch (−1) interpretiert werden (Bugfix), auch in der Engine?
