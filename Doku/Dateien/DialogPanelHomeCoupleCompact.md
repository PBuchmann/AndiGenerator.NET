# DialogPanelHomeCoupleCompact (DialogPanelHomeCoupleCompact.pas (keine .dfm))

**Kategorie:** UI-Panel (eingebettet) – dünner Wrapper (Master/Detail je Mannschaft)
**Umfang:** 51 Zeilen .pas / –
**Abhängigkeiten (uses):** DialogPanelMultiTeams (Basisklasse), DialogTeamPanel (`TDialogTeamPanelClass`), DialogPanelHomeCoupleCompactDetail (Detail-Panel), DialogPanel, PlanDataObjects, PlanTypes, PlanUtils; außerdem ungenutzt in uses: EditMandatoryDatesDialog, DialogPanelOptionArray, DialogForSingleGewichtungsOptions, DialogEditTeamName, System.UITypes; VCL-Standardunits
**Verwendet von:** AndiGeneratorMain (Panelliste in `ToolButtonDataClick`), DialogFirstStart (`StartAction(TDialogPanelHomeCoupleCompact)` im Schritt `tmKoppel`)

## Zweck
`TDialogPanelHomeCoupleCompact` ist ein **dünner Wrapper** um das Detail-Panel `TDialogPanelHomeCoupleCompactDetail`: Er erbt von `TDialogPanelMultiTeams` und liefert nur Caption, Header-Text und die Detail-Panel-Klasse. Die Basisklasse zeigt links eine Liste (TreeView) aller Mannschaften des Plans und rechts das Detail-Panel für die ausgewählte Mannschaft. Fachlich: „Heimkoppeltermine / Doppelspieltage“.

## Inhalt / Struktur
```pascal
TDialogPanelHomeCoupleCompact = class(TDialogPanelMultiTeams)
  function getDialogClass(): TDialogTeamPanelClass; override;  // Result := TDialogPanelHomeCoupleCompactDetail
  function getCaption(): String; override;                      // 'Heimkoppel/Doppelspieltage (Kompakt)'
  function getHeaderText(): String; override;                   // mehrzeiliger Hinweistext (s.u.)
end;
```
Header-Text (wird in `LabelHeaderText` über dem Detail-Panel angezeigt; leer → Header-Panel unsichtbar):
> Neue Heimkoppeltermine können nur in den Wunschterminen angelegt werden.<br>Die zweite Anfangszeit kann ebenfalls nur in den Wunschterminen angegeben werden

### Geerbtes Verhalten aus TDialogPanelMultiTeams (für das Verständnis des Musters)
- `FormCreate`: `Caption := getCaption`, `LabelHeaderText.Caption := getHeaderText`, Header ausblenden wenn leer, `FixControls`.
- `setPanel(Host)`: eigenes `PanelMain` in den Host umhängen.
- `DataToForm`: TreeView mit `PlanData.getTeamNames` (alphabetisch sortiert) füllen; ausgewählt wird die zuletzt bearbeitete Mannschaft (**unit-globale Variable `LastTeam`**, bleibt über Panel- und Dialoginstanzen hinweg erhalten) oder die erste. Die Selektion löst `TreeViewChange` → `ChangePanel(TeamName)` aus.
- `ChangePanel(TeamName)`: `LastTeam := TeamName`; altes Detail: `FormToData` + `FreeAndNil`; neues Detail: `getDialogClass.Create(Self)` → `setPanel(PanelDetail)` → `setPlanData(TeamName, PlanData, PlanDataDefault)` → `DataToForm` → `OnChange := DoOnDialogDetailChanged` (leitet an eigenes `FireOnChange` weiter); `PanelHeader.Caption := TeamName + ' / ' + getCaption`; `FireOnChange`.
- `TreeViewChanging`: Wechsel nur wenn `Detail.CheckData` True.
- `FormToData`, `toDefault`, `isDefault`, `CheckData`: werden **nur an das aktuell sichtbare Detail-Panel** delegiert (d.h. „Standard wiederherstellen“ wirkt nur auf die gerade gewählte Mannschaft; `isDefault` betrachtet nur diese).

## Fachliche Logik / Regeln
Fachregel „Heimkoppeltermine / Doppelspieltage“: zwei Heimspiele am selben Tag (Koppel) bzw. an zwei aufeinanderfolgenden Tagen (Doppelspieltag) mit Prioritäten möglich/gewünscht/hohe Prio. Details im Detail-Panel. Der Header weist darauf hin, dass neue Koppeltermine und zweite Anfangszeiten nur im Panel „Wunschtermine“ angelegt werden können.

## Daten & Persistenz
Keine eigene. Schreibzugriffe erfolgen im Detail-Panel `TDialogPanelHomeCoupleCompactDetail` auf die Arbeitskopie `PlanData` (DataObject-Baum), Persistenz über `*.modifications` (siehe DialogPanel.md).

Keine eigene .dfm; Layout aus `DialogPanelMultiTeams.dfm`.

## Threading / Performance
Keine. (Beim Mannschaftswechsel wird jeweils ein komplettes Detail-Formular neu erzeugt und zerstört – unkritisch.)

## Plattformabhängigkeiten
Keine eigenen; indirekt VCL über die Basisklasse (`TTreeView`, Reparenting von `TPanel`, Metaklasse `TDialogTeamPanelClass`).

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI.ViewModels` – keine eigene Klasse nötig; Instanz des generischen `PerTeamEditorPageViewModel<HomeCoupleTeamViewModel>` mit Parametern `Title = "Heimkoppel/Doppelspieltage (Kompakt)"`, `HeaderText = …` (siehe Muster in DialogPanel.md). Alternativ eine Factory-Registrierung `pages.Add(() => new PerTeamEditorPage<HomeCoupleTeamViewModel>("Heimkoppel/Doppelspieltage (Kompakt)", header))`.
- `LastTeam` (globale Variable) → Eigenschaft eines langlebigen UI-State-Services (`IEditorUiState.LastSelectedTeam`), nicht statisch.
- Delegation von `isDefault/toDefault` nur an das aktuelle Team beibehalten oder bewusst erweitern (Offene Frage).
- Header-Text als Ressource (lokalisierbar) ablegen; `cNewLine` → `Environment.NewLine`/`\n`.
- **Aufwand:** S – reine Konfiguration des generischen Master/Detail-ViewModels.
- **Auffälligkeiten:** `cNewLine` stammt aus PlanUtils/PlanTypes (Zeilenumbruch). Viele ungenutzte Units in der uses-Klausel (Copy & Paste).

## Offene Fragen
- Soll „Standard wiederherstellen“ im Master/Detail-Panel weiterhin nur die aktuell gewählte Mannschaft zurücksetzen, oder alle Mannschaften?
