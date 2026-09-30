# DialogPanel (DialogPanel.pas + DialogPanel.dfm)

**Kategorie:** UI-Panel (eingebettet) – abstrakte Basisklasse des Panel-Frameworks
**Umfang:** 67 Zeilen .pas / 196 Zeilen .dfm (die .dfm ist **verwaist**, siehe unten)
**Abhängigkeiten (uses):** PlanTypes, PlanUtils, PlanDataObjects; VCL (Forms, ExtCtrls, StdCtrls, ComCtrls, Grids, ValEdit, Menus), Winapi.Windows/Messages
**Verwendet von:** DialogTeamPanel, DialogPanelMultiTeams, DialogForSinglePanel, DialogForMultiplePanel, DialogFirstStart, DialogEditSisterTeam, DialogForSingleGewichtungsOptions (nur uses), sowie **alle** `DialogPanel*`-Units (MainData, Teams, Ranking, LocationsCompact(+Detail), HomeCoupleCompact(+Detail), Auswaertskoppel(+Detail), HomeDays(+Detail), SisterTeams(+Detail), 60km(+Detail), FreeDays, MandatoryDays, PredefinedGames, HomeRight(+Detail))

## Zweck
`TDialogPanel` ist die abstrakte Basisklasse aller „Datenbearbeitungs-Panels“ im Dialog *Daten bearbeiten* (Toolbutton „Daten“ im Hauptfenster) und im Erststart-Assistenten. Jedes Panel ist technisch ein `TForm`, dessen Inhalts-Panel (`PanelMain`) zur Laufzeit per `setPanel` in einen Host-Container umgehängt wird (Reparenting). Die Basisklasse definiert den Lebenszyklus *Laden → Bearbeiten → Validieren → Zurückschreiben* sowie *Standard wiederherstellen / Ist Standard?* gegen einen Referenzdatenstand (`PlanDataDefault` = unveränderter Click-TT-Import).

## Inhalt / Struktur
```pascal
TDialogPanelClass = class of TDialogPanel;          // Metaklasse → Host instanziiert per Klasse
TDialogPanel = class(TForm)
  private  FOnChange: TNotifyEvent;
  protected procedure FireOnChange();                // ruft OnChange(Self), falls zugewiesen
  public
    PlanData: TPlanData;                             // Arbeitskopie (vom Host besessen!)
    PlanDataDefault: TPlanData;                      // Referenz = Click-TT-Originaldaten (kann nil sein)
    procedure setPanel(PanelMain: TPanel); virtual; abstract;   // eigenes PanelMain.Parent := Host-Panel
    procedure setPlanData(PlanData, PlanDataDefault: TPlanData);// nur Referenzen merken
    procedure DataToForm(); virtual; abstract;       // PlanData → Controls
    procedure FormToData(); virtual; abstract;       // Controls → PlanData (DataObject-Baum)
    procedure toDefault(); virtual; abstract;        // Controls (und z.T. PlanData) auf PlanDataDefault zurücksetzen
    function  isDefault(): boolean; virtual; abstract; // entspricht aktueller Stand dem Default?
    function  CheckData(): boolean; virtual;         // Validierung; Basis: True
    property  OnChange: TNotifyEvent;                // Host abonniert → Buttons aktualisieren
end;
```

### Klassenhierarchie des Frameworks (projektweit)
```
TForm
 └─ TDialogPanel                         (DialogPanel.pas)       – globale Panels (ganzer Plan)
     ├─ TDialogPanelFreeDays, TDialogPanelMainData, TDialogPanelTeams, TDialogPanelRanking,
     │  TDialogPanelMandatoryDays, TDialogPanelPredefinedGames  – direkt abgeleitet
     ├─ TDialogTeamPanel                 (DialogTeamPanel.pas)   – + TeamName; Basis der *Detail-Panels
     │   ├─ TDialogPanel60kmDetail, TDialogPanelAuswaertsKoppelDetail, TDialogPanelHomeCoupleCompactDetail,
     │   │  TDialogPanelHomeDaysDetail, TDialogPanelHomeRightDetail, TDialogPanelLocationsCompactDetail,
     │   │  TDialogPanelSisterTeamsDetail
     └─ TDialogPanelMultiTeams           (DialogPanelMultiTeams.pas) – Master/Detail: TreeView mit Mannschaften
         └─ dünne Wrapper (nur 3 Overrides, keine eigene .dfm):
            TDialogPanel60km, TDialogPanelAuswaertskoppel, TDialogPanelHomeCoupleCompact,
            TDialogPanelHomeDays, TDialogPanelHomeRight, TDialogPanelLocationsCompact, TDialogPanelSisterTeams
```

### Host-Dialoge (nicht Teil dieser Unit, aber für das Muster maßgeblich)
- **TDialogForMultiplePanel** (`SetValues(SourcePlan, PlanDataDefault, array of TDialogPanelClass)`): TreeView links mit einem Knoten je Panelklasse (Caption wird ermittelt, indem kurz eine Instanz erzeugt und wieder freigegeben wird), rechts das aktive Panel. Buttons OK / Abbrechen / „Standard wiederherstellen“ (nur sichtbar, wenn `PlanDataDefault <> nil`).
- **TDialogForSinglePanel** (`SetValues(SourcePlan, PlanDataDefault, PanelClass)`): nur ein Panel, genutzt im Erststart-Assistenten (`DialogFirstStart.StartAction`).
- Aufrufer: `AndiGeneratorMain.ToolButtonDataClick` mit Reihenfolge `[MainData, Teams, Ranking, LocationsCompact, HomeCoupleCompact, Auswaertskoppel, HomeDays, SisterTeams, 60km, FreeDays, MandatoryDays, PredefinedGames, HomeRight]` und `PlanDataDefault = PlanDataClickTTFile`.

### Lebenszyklus (exakt, wie im Code)
1. **Host öffnen:** Host erzeugt eigene Arbeitskopie `PlanData := TPlanData.Create; PlanData.Assign(SourcePlan)` (Deep Copy des DataObject-Baums).
2. **Panel aktivieren** (`ChangePanel`): falls altes Panel vorhanden → `old.FormToData(); FreeAndNil(old)`. Dann `P := PanelClass.Create(Host)` → `P.setPanel(HostPanel)` → `P.setPlanData(PlanData, PlanDataDefault)` → `P.DataToForm()` → `P.OnChange := Host.DoOnDialogPanelChanged` → `EnableButtons` (ruft `isDefault`).
3. **Bearbeiten:** Control-Events rufen `FireOnChange` → Host `EnableButtons` → `ButtonToStandard.Enabled := not Panel.isDefault`. **Achtung:** Viele Implementierungen von `isDefault` rufen intern `FormToData()` auf (Seiteneffekt!), d.h. die Arbeitskopie wird bei *jeder* Änderung bereits aktualisiert.
4. **Panelwechsel (TreeView.OnChanging):** `if not Panel.CheckData then AllowChange := False` – Validierung blockiert den Wechsel.
5. **OK:** MultiplePanel: `if Panel.CheckData then begin Panel.FormToData; ModalResult := mrOk end`. SinglePanel: **ohne** `CheckData` direkt `FormToData` (Inkonsistenz).
6. **Übernahme beim Aufrufer:** `PlanData.Assign(Form.getPlanData())` + `DoOnAfterPlanChanged(nil, false)` → Options speichern, `SavePlan()` (schreibt `*.modifications` als Diff), Plan neu laden, Optimizer mit neuen Daten versorgen.
7. **Abbrechen:** Arbeitskopie wird im `FormDestroy` des Hosts verworfen (`PlanData.Free`).
8. **Standard wiederherstellen:** `Panel.toDefault()` → Host `EnableButtons`.

### Wie Änderungen in TPlanData / Modifications landen
- Panels schreiben **direkt in den generischen DataObject-Baum** (`PlanData.root` → `team`-Knoten → Kindknoten `homegameday`, `nogameday`, `roadcouple`, `noweekgames`, `sisterteam`, `homerights` …) bzw. über die Hilfsfunktionen `TPlanData.GetHomeDays/SetHomeDays(TeamName, THomeDayList)`.
- Typisches Schreibmuster: `TeamNode.deleteChilds(<Knotenname>)` und danach alle Kindknoten neu erzeugen (`TDataObject.Create(Name, Parent)` hängt sich selbst an den Parent).
- Persistenz: Bei Click-TT-Plänen wird **nicht** der ganze Baum gespeichert, sondern `DiffData.Diff(PlanData, PlanDataClickTTFile)` → Datei `<Plan>.modifications` (XML). Knoten tragen dabei ein `state`-Attribut (`dosNew`, `dosModified`, `dosDelete`), Identifikation über Schlüsselattribute aus `getKeysForDataType` (z.B. `team`→`teamname`, `homegameday`→`datetime`, `roadcouple`→`teamnamea+teamnameb`, `homerights`→`teamname`, `sisterteam`→`teamname+gender`). Beim Laden: `PlanData.Merge(DiffData)`.
- Da `SetHomeDays` die Knoten komplett neu anlegt (mit *allen* bekannten Attributen), können im Diff Attribute als „modified“ auftauchen, die vorher nur fehlten (z.B. `parallelgames="0"` vs. nicht vorhanden). Offene Frage, ob das gewollt ist.

## Fachliche Logik / Regeln
Keine in der Basisklasse. Die Referenzsemantik „Default = Stand aus der Click-TT-Datei“ ist das zentrale Konzept für „Standard wiederherstellen“.

## Daten & Persistenz
Keine direkte Persistenz. Indirekt: `*.modifications` (Diff-XML) über den Aufrufer.

**DialogPanel.dfm ist verwaist:** `DialogPanel.pas` enthält kein `{$R *.dfm}`. Die .dfm beschreibt ein altes Formular `FormKoppelDays: TFormKoppelDays` (Tabs „Heimkoppeltage (Doppelspieltage)“, „Auswärtskoppelwünsche“, „Anfangszeiten Auswärtskoppeltage“, Buttons OK/Standard/Abbrechen, Popup „Alles (de)selektieren“). Die Klasse `TFormKoppelDays` existiert im Projekt nicht mehr → **nicht migrieren**, nur als historischer Hinweis auf die Vorgänger-UI der Koppel-Panels.

## Threading / Performance
Keine. UI-Thread. Der Optimizer wird vom Aufrufer (`ToolButtonDataClick`) während des modalen Dialogs pausiert (`Optimizer.Paused := True`) und danach auf den vorherigen Zustand zurückgesetzt.

## Plattformabhängigkeiten
- VCL-spezifisch: Panel = `TForm`, Reparenting von `PanelMain` in fremde Container, Metaklassen (`class of`) zur Instanziierung.
- `FixControls` (PlanUtils) passt Fonts/Tab-Order an (VCL).

## Migrationshinweise für C#
- **Ziel:** `AndiGenerator.UI` (Avalonia UI empfohlen für Win/macOS/Linux; MAUI falls Mobile Pflicht) + `AndiGenerator.UI.ViewModels`.
- **Mapping auf wiederverwendbares MVVM-Muster:**
  ```csharp
  public interface IEditorPage {                  // ~ TDialogPanel
      string Title { get; }                        // Caption
      void Load(PlanDataModel work, PlanDataModel? reference);   // setPlanData + DataToForm
      void Commit();                               // FormToData
      void ResetToDefault();                       // toDefault
      bool IsDefault { get; }                      // isDefault – OHNE Seiteneffekt berechnen!
      bool Validate(out string? message);          // CheckData – Meldung zurückgeben statt ShowMessage
      event EventHandler? Changed;                 // OnChange (bzw. INotifyPropertyChanged)
  }
  abstract class EditorPageViewModel : ObservableObject, IEditorPage { ... }
  abstract class TeamEditorPageViewModel : EditorPageViewModel { string TeamName; }   // ~ TDialogTeamPanel
  class PerTeamEditorPageViewModel<TDetail> : EditorPageViewModel                        // ~ TDialogPanelMultiTeams
      where TDetail : TeamEditorPageViewModel, new() { ObservableCollection<string> Teams; string SelectedTeam; TDetail Detail; string HeaderText; }
  class EditorHostViewModel { ObservableCollection<IEditorPage> Pages; IEditorPage Current; ICommand Ok, Cancel, ResetToDefault; }  // ~ TDialogForMultiplePanel/SinglePanel
  ```
  Views werden per `DataTemplate` (ViewModel→View) aufgelöst statt per Reparenting; die Metaklassen-Liste wird zu einer Liste von Factory-Delegates `Func<IEditorPage>`.
- **Arbeitskopie/Transaktion:** Deep-Clone des Datenmodells beim Öffnen, bei OK zurückkopieren – beibehalten (einfach und robust).
- **Fallstricke:**
  - `isDefault` mit Seiteneffekt (`FormToData`) nicht nachbauen; stattdessen ViewModel-Zustand direkt mit Referenz vergleichen.
  - `PlanDataDefault` kann `nil` sein (selbst erstellter Plan ohne Click-TT) → alle Default-Vergleiche null-sicher implementieren (mehrere Detail-Panels stürzen im Original sonst ab).
  - OK im SinglePanel-Host ruft kein `CheckData` → in C# einheitlich validieren.
  - Ereignisreihenfolge: Beim Panelwechsel wird das alte Panel vor dem Freigeben gespeichert; die Validierung erfolgt vorher in `OnChanging`.
- **Aufwand:** S – die Basisklasse selbst ist trivial; der Aufwand steckt im Gesamtframework (Host + MultiTeams ≈ M).

## Offene Fragen
- Soll die „Diff-gegen-Click-TT“-Speicherung (`*.modifications`) in C# beibehalten werden (Kompatibilität mit bestehenden Dateien der Anwender)?
- Soll `CheckData` auch im SinglePanel-Host (Erststart) bei OK erzwungen werden?
