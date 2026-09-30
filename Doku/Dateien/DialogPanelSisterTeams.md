# DialogPanelSisterTeams (DialogPanelSisterTeams.pas, keine eigene .dfm – erbt die von DialogPanelMultiTeams)

**Kategorie:** UI-Panel (eingebettet)
**Umfang:** 50 Zeilen .pas / keine .dfm
**Abhängigkeiten (uses):** DialogPanelMultiTeams, DialogTeamPanel, DialogPanelSisterTeamsDetail (fachlich genutzt); viele weitere uses (EditMandatoryDatesDialog, DialogPanelOptionArray, DialogForSingleGewichtungsOptions, DialogEditTeamName …) sind Copy-&-Paste ohne Nutzung
**Verwendet von:** AndiGeneratorMain (Liste der Panels für `TDialogForMultiplePanel` in `ToolButtonDataClick`)

## Zweck
Konkrete Seite „**Spiele der Nachbarmannschaften**" im Daten-Dialog. Nachbarmannschaften (Sister Teams) sind Mannschaften desselben Vereins in **anderen** Ligen, deren Termine bei der Planung berücksichtigt werden (z. B. Hallenbelegung, keine gleichzeitigen Spiele). Die Unit ist eine reine Konfiguration des Containers `TDialogPanelMultiTeams`.

## Inhalt / Struktur
- `TDialogPanelSisterTeams = class(TDialogPanelMultiTeams)`
  - `getCaption` → `'Spiele der Nachbarmannschaften'`
  - `getDialogClass` → `TDialogPanelSisterTeamsDetail`
  - `getHeaderText` → `''` (kein Hinweistext; Hinweis-Panel wird ausgeblendet)
- Keine .dfm und kein `{$R *.dfm}`: VCL lädt beim Erzeugen die Ressource der Vorfahrenklasse (`TDialogPanelMultiTeams`) über `InitInheritedComponent` – funktioniert, weil eine Vorfahren-Ressource existiert.

**Muster:** Template-Method – typische Subklasse des Panel-Frameworks (identisch aufgebaut: DialogPanel60km, DialogPanelHomeDays, DialogPanelHomeRight, DialogPanelAuswaertskoppel, DialogPanelLocationsCompact, DialogPanelHomeCoupleCompact).

## Fachliche Logik / Regeln
Keine (siehe DialogPanelSisterTeamsDetail).

## Daten & Persistenz
Keine direkt.

## Threading / Performance
Keine.

## Plattformabhängigkeiten
Nur indirekt über VCL-Vererbung / dfm-Vererbung.

## Migrationshinweise für C#
- Ziel: Entfällt als eigene Klasse; Registrierung z. B. `new MultiTeamPanelDescriptor("Spiele der Nachbarmannschaften", headerText: null, () => new SisterTeamsDetailViewModel())` oder `sealed class SisterTeamsPanelViewModel : MultiTeamPanelViewModel<SisterTeamsDetailViewModel>`.
- Aufwand: **S**.

## Offene Fragen
Keine.
