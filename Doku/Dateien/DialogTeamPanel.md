# DialogTeamPanel (DialogTeamPanel.pas, keine .dfm)

**Kategorie:** UI-Panel (eingebettet) – abstrakte Basisklasse
**Umfang:** 45 Zeilen .pas / keine .dfm
**Abhängigkeiten (uses):** DialogPanel, PlanTypes, PlanUtils, PlanDataObjects; VCL (Forms, ExtCtrls …) – überwiegend Copy-&-Paste-uses ohne Nutzung
**Verwendet von:** DialogPanelMultiTeams, DialogPanelSisterTeams, DialogPanelSisterTeamsDetail, DialogPanel60km, DialogPanel60kmDetail, DialogPanelAuswaertskoppel, DialogPanelAuswaertsKoppelDetail, DialogPanelHomeCoupleCompact(+Detail), DialogPanelHomeDays(+Detail), DialogPanelHomeRight(+Detail), DialogPanelLocationsCompact(+Detail)

## Zweck
Basisklasse für alle **mannschaftsbezogenen Detail-Panels** des Panel-Frameworks. Sie erweitert `TDialogPanel` lediglich um den Namen der aktuell bearbeiteten Mannschaft (`TeamName`), damit ein Detail-Panel die Daten genau einer Mannschaft aus dem gemeinsamen `TPlanData`-Baum lesen/schreiben kann. Wird ausschließlich vom Container `TDialogPanelMultiTeams` instanziiert.

## Inhalt / Struktur
- `TDialogTeamPanelClass = class of TDialogTeamPanel` – Metaklasse (Klassenreferenz), damit `TDialogPanelMultiTeams.getDialogClass()` den konkreten Detail-Typ liefern kann (Factory-Muster über Klassenreferenz).
- `TDialogTeamPanel = class(TDialogPanel)`
  - `TeamName: String` (public Feld)
  - `setPlanData(TeamName, PlanData, PlanDataDefault)` – setzt `TeamName` und ruft die geerbte `setPlanData(PlanData, PlanDataDefault)` auf (Overload, nicht virtuell).
- Alle abstrakten Methoden von `TDialogPanel` (`setPanel`, `DataToForm`, `FormToData`, `toDefault`, `isDefault`) bleiben abstrakt; `CheckData` bleibt `True`.

### Panel-Framework im Überblick (Kontext)
```
TForm
 └─ TDialogPanel            (DialogPanel.pas)  – Vertrag: setPanel/DataToForm/FormToData/toDefault/isDefault/CheckData, OnChange
     ├─ TDialogPanelMainData, ...Teams, ...Ranking, ...MandatoryDays, ...PredefinedGames, ...
     ├─ TDialogPanelMultiTeams  – Container: Mannschafts-TreeView links + ein TDialogTeamPanel rechts
     │    └─ TDialogPanelSisterTeams, ...60km, ...HomeDays, ...  (liefern nur getDialogClass/Caption/HeaderText)
     └─ TDialogTeamPanel        – + TeamName
          └─ TDialogPanelSisterTeamsDetail, ...60kmDetail, ...HomeDaysDetail, ...
Host-Dialoge: TDialogForMultiplePanel (TreeView aller Panels), TDialogForSinglePanel (ein Panel)
```

## Fachliche Logik / Regeln
Keine.

## Daten & Persistenz
Keine direkt; Subklassen lokalisieren über `TeamName` den `<team teamname="…">`-Knoten im `TPlanData`-Baum.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
Erbt von `TForm` (VCL). Das Konzept „Formular erzeugen, nur dessen `PanelMain` in einen fremden Container umhängen" ist VCL-spezifisch.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI` – abstrakte ViewModel-Basis `TeamDetailPanelViewModel : DialogPanelViewModel` mit `string TeamName { get; init; }`; View als `UserControl` (Avalonia UI empfohlen für Windows/macOS/Linux, ggf. .NET MAUI für Mobile).
- Metaklasse `class of` → `Func<TeamDetailPanelViewModel>` bzw. `Type` + `Activator`/DI-Factory, oder generischer Container `MultiTeamPanelViewModel<TDetail> where TDetail : TeamDetailPanelViewModel, new()`.
- `setPlanData`-Overload besser als Konstruktor-Parameter/Init-Methode.
- Aufwand: **S** – triviale Klasse.

## Offene Fragen
Keine.
