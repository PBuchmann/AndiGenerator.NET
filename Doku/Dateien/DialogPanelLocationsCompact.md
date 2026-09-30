# DialogPanelLocationsCompact (DialogPanelLocationsCompact.pas (keine .dfm))

**Kategorie:** UI-Panel (eingebettet) – dünner Wrapper (Master/Detail je Mannschaft)
**Umfang:** 50 Zeilen .pas / –
**Abhängigkeiten (uses):** DialogPanelMultiTeams (Basisklasse), DialogTeamPanel (`TDialogTeamPanelClass`), DialogPanelLocationsCompactDetail (Detail-Panel), DialogPanel, PlanDataObjects, PlanTypes, PlanUtils; außerdem ungenutzt in uses: EditMandatoryDatesDialog, DialogPanelOptionArray, DialogForSingleGewichtungsOptions, DialogEditTeamName, System.UITypes; VCL-Standardunits
**Verwendet von:** AndiGeneratorMain (Panelliste in `ToolButtonDataClick`), DialogFirstStart (`StartAction(TDialogPanelLocationsCompact)` im Schritt `tmLocations`)

## Zweck
`TDialogPanelLocationsCompact` ist ein **dünner Wrapper** um das Detail-Panel `TDialogPanelLocationsCompactDetail`: Er erbt von `TDialogPanelMultiTeams` und liefert nur Caption, Header-Text und die Detail-Panel-Klasse. Die Basisklasse zeigt links eine Liste (TreeView) aller Mannschaften des Plans und rechts das Detail-Panel für die ausgewählte Mannschaft. Fachlich: „Spiellokale / Hallenbelegung“.

## Inhalt / Struktur
```pascal
TDialogPanelLocationsCompact = class(TDialogPanelMultiTeams)
  function getDialogClass(): TDialogTeamPanelClass; override;  // Result := TDialogPanelLocationsCompactDetail
  function getCaption(): String; override;                      // 'Spiellokale (Kompaktansicht)'
  function getHeaderText(): String; override;                   // '' (leer)
end;
```
Header-Text (wird in `LabelHeaderText` über dem Detail-Panel angezeigt; leer → Header-Panel unsichtbar):
> *(leer)*

### Geerbtes Verhalten aus TDialogPanelMultiTeams (für das Verständnis des Musters)
- `FormCreate`: `Caption := getCaption`, `LabelHeaderText.Caption := getHeaderText`, Header ausblenden wenn leer, `FixControls`.
- `setPanel(Host)`: eigenes `PanelMain` in den Host umhängen.
- `DataToForm`: TreeView mit `PlanData.getTeamNames` (alphabetisch sortiert) füllen; ausgewählt wird die zuletzt bearbeitete Mannschaft (**unit-globale Variable `LastTeam`**, bleibt über Panel- und Dialoginstanzen hinweg erhalten) oder die erste. Die Selektion löst `TreeViewChange` → `ChangePanel(TeamName)` aus.
- `ChangePanel(TeamName)`: `LastTeam := TeamName`; altes Detail: `FormToData` + `FreeAndNil`; neues Detail: `getDialogClass.Create(Self)` → `setPanel(PanelDetail)` → `setPlanData(TeamName, PlanData, PlanDataDefault)` → `DataToForm` → `OnChange := DoOnDialogDetailChanged` (leitet an eigenes `FireOnChange` weiter); `PanelHeader.Caption := TeamName + ' / ' + getCaption`; `FireOnChange`.
- `TreeViewChanging`: Wechsel nur wenn `Detail.CheckData` True.
- `FormToData`, `toDefault`, `isDefault`, `CheckData`: werden **nur an das aktuell sichtbare Detail-Panel** delegiert (d.h. „Standard wiederherstellen“ wirkt nur auf die gerade gewählte Mannschaft; `isDefault` betrachtet nur diese).

## Fachliche Logik / Regeln
Fachregel „Spiellokale / Hallenbelegung“: Standardspiellokal je Mannschaft, abweichende Lokale je Heimspieltag und Lokale der Nachbarmannschaften (Vereinsmannschaften anderer Ligen, die dieselbe Halle nutzen). Details im Detail-Panel.

## Daten & Persistenz
Keine eigene. Schreibzugriffe erfolgen im Detail-Panel `TDialogPanelLocationsCompactDetail` auf die Arbeitskopie `PlanData` (DataObject-Baum), Persistenz über `*.modifications` (siehe DialogPanel.md).

Keine eigene .dfm; Layout aus `DialogPanelMultiTeams.dfm`; Header ausgeblendet (leer).

## Threading / Performance
Keine. (Beim Mannschaftswechsel wird jeweils ein komplettes Detail-Formular neu erzeugt und zerstört – unkritisch.)

## Plattformabhängigkeiten
Keine eigenen; indirekt VCL über die Basisklasse (`TTreeView`, Reparenting von `TPanel`, Metaklasse `TDialogTeamPanelClass`).

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels` – keine eigene Klasse nötig; Instanz des generischen `PerTeamEditorPageViewModel<LocationsTeamViewModel>` mit Parametern `Title = "Spiellokale (Kompaktansicht)"`, `HeaderText = …` (siehe Muster in DialogPanel.md). Alternativ eine Factory-Registrierung `pages.Add(() => new PerTeamEditorPage<LocationsTeamViewModel>("Spiellokale (Kompaktansicht)", header))`.
- `LastTeam` (globale Variable) → Eigenschaft eines langlebigen UI-State-Services (`IEditorUiState.LastSelectedTeam`), nicht statisch.
- Delegation von `isDefault/toDefault` nur an das aktuelle Team beibehalten oder bewusst erweitern (Offene Frage).
- Header-Text als Ressource (lokalisierbar) ablegen; `cNewLine` → `Environment.NewLine`/`\n`.
- **Aufwand:** S – reine Konfiguration des generischen Master/Detail-ViewModels.
- **Auffälligkeiten:** Viele ungenutzte Units in der uses-Klausel (Copy & Paste).

## Offene Fragen
- Soll „Standard wiederherstellen“ im Master/Detail-Panel weiterhin nur die aktuell gewählte Mannschaft zurücksetzen, oder alle Mannschaften?
