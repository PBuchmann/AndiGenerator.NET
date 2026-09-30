# DialogPanel60km (DialogPanel60km.pas + DialogPanel60km.dfm)

**Kategorie:** UI-Panel (eingebettet) – dünner Wrapper (Master/Detail je Mannschaft)
**Umfang:** 50 Zeilen .pas / 79 Zeilen .dfm (verwaist)
**Abhängigkeiten (uses):** DialogPanelMultiTeams (Basisklasse), DialogTeamPanel (`TDialogTeamPanelClass`), DialogPanel60kmDetail (Detail-Panel), DialogPanel, PlanDataObjects, PlanTypes, PlanUtils; außerdem ungenutzt in uses: EditMandatoryDatesDialog, DialogPanelOptionArray, DialogForSingleGewichtungsOptions, DialogEditTeamName, System.UITypes; VCL-Standardunits
**Verwendet von:** AndiGeneratorMain (Panelliste in `ToolButtonDataClick`)

## Zweck
`TDialogPanel60km` ist ein **dünner Wrapper** um das Detail-Panel `TDialogPanel60kmDetail`: Er erbt von `TDialogPanelMultiTeams` und liefert nur Caption, Header-Text und die Detail-Panel-Klasse. Die Basisklasse zeigt links eine Liste (TreeView) aller Mannschaften des Plans und rechts das Detail-Panel für die ausgewählte Mannschaft. Fachlich: „60-km-Regel“.

## Inhalt / Struktur
```pascal
TDialogPanel60km = class(TDialogPanelMultiTeams)
  function getDialogClass(): TDialogTeamPanelClass; override;  // Result := TDialogPanel60kmDetail
  function getCaption(): String; override;                      // '60km Regel'
  function getHeaderText(): String; override;                   // mehrzeiliger Hinweistext (s.u.)
end;
```
Header-Text (wird in `LabelHeaderText` über dem Detail-Panel angezeigt; leer → Header-Panel unsichtbar):
> Wegen des langen Anfahrtsweges dürfen Spiele gegen diese Mannschaften nur am Wochenende stattfinden

### Geerbtes Verhalten aus TDialogPanelMultiTeams (für das Verständnis des Musters)
- `FormCreate`: `Caption := getCaption`, `LabelHeaderText.Caption := getHeaderText`, Header ausblenden wenn leer, `FixControls`.
- `setPanel(Host)`: eigenes `PanelMain` in den Host umhängen.
- `DataToForm`: TreeView mit `PlanData.getTeamNames` (alphabetisch sortiert) füllen; ausgewählt wird die zuletzt bearbeitete Mannschaft (**unit-globale Variable `LastTeam`**, bleibt über Panel- und Dialoginstanzen hinweg erhalten) oder die erste. Die Selektion löst `TreeViewChange` → `ChangePanel(TeamName)` aus.
- `ChangePanel(TeamName)`: `LastTeam := TeamName`; altes Detail: `FormToData` + `FreeAndNil`; neues Detail: `getDialogClass.Create(Self)` → `setPanel(PanelDetail)` → `setPlanData(TeamName, PlanData, PlanDataDefault)` → `DataToForm` → `OnChange := DoOnDialogDetailChanged` (leitet an eigenes `FireOnChange` weiter); `PanelHeader.Caption := TeamName + ' / ' + getCaption`; `FireOnChange`.
- `TreeViewChanging`: Wechsel nur wenn `Detail.CheckData` True.
- `FormToData`, `toDefault`, `isDefault`, `CheckData`: werden **nur an das aktuell sichtbare Detail-Panel** delegiert (d.h. „Standard wiederherstellen“ wirkt nur auf die gerade gewählte Mannschaft; `isDefault` betrachtet nur diese).

## Fachliche Logik / Regeln
Fachregel „60-km-Regel“: Spiele gegen die im Detail-Panel angehakten Gegner dürfen nur am Wochenende stattfinden (ob Freitag zum Wochenende zählt, regelt `FreitagIsAllowedBy60km` in den Einstellungen, siehe DialogOptions). Die eigentliche Regel steckt im Detail-Panel/Engine.

## Daten & Persistenz
Keine eigene. Schreibzugriffe erfolgen im Detail-Panel `TDialogPanel60kmDetail` auf die Arbeitskopie `PlanData` (DataObject-Baum), Persistenz über `*.modifications` (siehe DialogPanel.md).

**DialogPanel60km.dfm ist verwaist:** Die .pas enthält kein `{$R *.dfm}`; zur Laufzeit wird die .dfm der Basisklasse `TDialogPanelMultiTeams` geladen. Die verwaiste .dfm beschreibt ein altes Layout (`ListBoxTeams`, Buttons „Neue Mannschaft…“, „Mannschaft bearbeiten…“, „Mannschaft Löschen…“ ohne Event-Handler) und ist offenbar eine Kopie eines anderen Panels → **nicht migrieren**.

## Threading / Performance
Keine. (Beim Mannschaftswechsel wird jeweils ein komplettes Detail-Formular neu erzeugt und zerstört – unkritisch.)

## Plattformabhängigkeiten
Keine eigenen; indirekt VCL über die Basisklasse (`TTreeView`, Reparenting von `TPanel`, Metaklasse `TDialogTeamPanelClass`).

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels` – keine eigene Klasse nötig; Instanz des generischen `PerTeamEditorPageViewModel<NoWeekGamesTeamViewModel>` mit Parametern `Title = "60km Regel"`, `HeaderText = …` (siehe Muster in DialogPanel.md). Alternativ eine Factory-Registrierung `pages.Add(() => new PerTeamEditorPage<NoWeekGamesTeamViewModel>("60km Regel", header))`.
- `LastTeam` (globale Variable) → Eigenschaft eines langlebigen UI-State-Services (`IEditorUiState.LastSelectedTeam`), nicht statisch.
- Delegation von `isDefault/toDefault` nur an das aktuelle Team beibehalten oder bewusst erweitern (Offene Frage).
- Header-Text als Ressource (lokalisierbar) ablegen; `cNewLine` → `Environment.NewLine`/`\n`.
- **Aufwand:** S – reine Konfiguration des generischen Master/Detail-ViewModels.
- **Auffälligkeiten:** Die Beziehung wird **einseitig** beim bearbeiteten Team gespeichert (`noweekgames`-Kinder). Ob die Engine sie symmetrisch auswertet, ist im Detail-Panel als offene Frage vermerkt. Viele ungenutzte Units in der uses-Klausel (Copy & Paste).

## Offene Fragen
- Soll „Standard wiederherstellen“ im Master/Detail-Panel weiterhin nur die aktuell gewählte Mannschaft zurücksetzen, oder alle Mannschaften?
