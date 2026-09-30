# DialogEditOneAuswaertsKoppel (DialogEditOneAuswaertsKoppel.pas + DialogEditOneAuswaertsKoppel.dfm)

**Kategorie:** UI-Dialog
**Umfang:** 127 Zeilen .pas / 121 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `PlanTypes`, `PlanUtils` (`FixControls`), `PlanDataObjects` (`TPlanData`, `TDataObject`, Attributkonstanten); VCL: `Forms`, `StdCtrls`, `ExtCtrls`, `ComCtrls`, `Grids`, `Dialogs`
**Verwendet von:** `DialogPanelAuswaertsKoppelDetail` (Neu anlegen Z. ~353, Bearbeiten Z. ~333)

## Zweck
Kleiner modaler Editor für **einen** Auswärtskoppelwunsch einer Mannschaft: Die Hauptmannschaft möchte ihre Auswärtsspiele gegen zwei andere Mannschaften (A und B) koppeln – entweder am gleichen Tag oder mit Übernachtung. Der Dialog arbeitet direkt auf einem generischen `TDataObject`-Knoten (`nRoadCouple` = `roadcouple`).

## Inhalt / Struktur
- `TDialogEditOneAuswaertsKoppel = class(TForm)`
- `SetValue(PlanData, Value: TDataObject, MainTeamName)` – füllt `ComboTeamA`/`ComboTeamB` mit allen Teamnamen (sortiert, `PlanData.getTeamNames`) **ohne** die Hauptmannschaft; ist `Value` gesetzt, werden `aTeamNameA`, `aTeamNameB`, `aSameDay` vorselektiert (Bearbeitungsmodus), sonst Neuanlage.
- `GetValue(Value: TDataObject)` – schreibt `teamnamea`, `teamnameb` (Strings) und `sameday` (Int = ItemIndex von `ComboArt`).
- `ButtonOKClick` – Validierung (s.u.), bei Erfolg `mrOk`.
- `EnableButtons` – leer (Platzhalter); `ComboTeamAChange` ruft es für alle drei Combos.
- `FormCreate` → `FixControls`.

UI (.dfm, Caption „Auswärtskoppelwunsch", 409×212):
- `ComboTeamA` „Manschaft 1:" [sic], `ComboTeamB` „Manschaft 2:" (beide `csDropDownList`)
- `ComboArt` „Art:" mit Items: 0 = „am gleichen Tag", 1 = „mit Übernachtung", 2 = „am gleichen Tag oder mit Übernachtung", 3 = '' (leerer Eintrag!)
- OK (Default) / Abbrechen (Cancel, `ModalResult=2`).

## Fachliche Logik / Regeln
- Validierung: Mannschaft 1 gewählt; Mannschaft 2 gewählt; Mannschaft 1 ≠ Mannschaft 2 (Vergleich per ItemIndex – gleiche Liste, daher äquivalent zum Namensvergleich).
- Hauptmannschaft kann nicht Koppelpartner sein (aus Liste entfernt).
- `aSameDay`-Kodierung: 0 = gleicher Tag, 1 = Übernachtung, 2 = beides. Die fachliche Auswertung erfolgt in `PlanTypes`/`PlanDataObjects` (nicht hier).

## Daten & Persistenz
Keine direkte. Ändert den übergebenen `TDataObject` im Speicher; persistiert wird später über `TPlanData.SaveToXML`/Diff-Datei (Attribute `teamnamea`, `teamnameb`, `sameday` an Knoten `roadcouple`).

## Threading / Performance
Keine.

## Plattformabhängigkeiten
Nur VCL-Formular, `ShowMessage`.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI` → `EditAuswaertsKoppelViewModel` + View (Avalonia empfohlen). Modell: statt generischem `TDataObject` ein typisiertes Record `RoadCouple(string TeamA, string TeamB, RoadCoupleKind Kind)` mit `enum RoadCoupleKind { SameDay=0, Overnight=1, Both=2 }` (Integer-Werte für XML-Kompatibilität beibehalten).
- Validierung als `INotifyDataErrorInfo` bzw. `CanExecute` des OK-Commands.
- Fallstricke: Wenn beim Bearbeiten ein gespeicherter Teamname nicht mehr existiert, ist ItemIndex −1 → OK wird blockiert (gewolltes Verhalten beibehalten). `ComboArt` enthält einen leeren 4. Eintrag (Index 3) – bei Auswahl würde `sameday=3` geschrieben.
- Aufwandsschätzung: **S** – einfacher Formulardialog mit drei Feldern.
- Auffälligkeiten: leerer 4. Eintrag in `ComboArt` (.dfm); `EnableButtons` leer; Label-Tippfehler „Manschaft".

## Offene Fragen
- Hat `sameday=3` (leerer Eintrag) eine Bedeutung oder ist das ein Designfehler? Vorschlag: entfernen.
