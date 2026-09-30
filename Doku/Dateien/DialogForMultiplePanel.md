# DialogForMultiplePanel (DialogForMultiplePanel.pas + DialogForMultiplePanel.dfm)

**Kategorie:** UI-Dialog (Framework-Host)
**Umfang:** 193 Zeilen .pas / 96 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `DialogPanel` (`TDialogPanel`, `TDialogPanelClass`), `PlanDataObjects` (`TPlanData`), `PlanUtils` (`FixFormSize`, `FixControls`), `PlanTypes`; VCL: `ComCtrls` (`TTreeView`), `ExtCtrls`, `StdCtrls`, `Forms`
**Verwendet von:** `AndiGeneratorMain` (`ToolButtonDataClick`, Z. ~655–690: Hauptdialog „Spielplandaten bearbeiten...")

## Zweck
Generischer **Mehrseiten-Editor** für die Spielplan-Eingabedaten: links eine flache TreeView-Navigation, rechts das jeweils aktive „DialogPanel". Alle Seiten arbeiten auf einer gemeinsamen **Arbeitskopie** von `TPlanData`; erst bei OK übernimmt der Aufrufer die Kopie. Die Seiten werden als Klassenreferenzen übergeben und lazy (bei Auswahl) instanziiert.

## Das Dialog-Framework (DialogPanel-Muster)
Dieses Muster zieht sich durch ~20 Units und sollte in C# als **ein** wiederverwendbares MVVM-Konstrukt abgebildet werden.

### Bausteine (Delphi)
1. **`TDialogPanel`** (Unit `DialogPanel`, abstrakt, erbt von `TForm`!): Jede Seite ist ein eigenes Formular mit einem Inhalts-`PanelMain`, das per `setPanel(Host)` **in das Host-Panel umgehängt** wird (`Self.PanelMain.Parent := HostPanel`). Vertrag:
   - `setPlanData(PlanData, PlanDataDefault)` – Arbeitskopie + Standarddaten (click-TT-Original) setzen
   - `DataToForm()` / `FormToData()` – Modell → UI bzw. UI → Modell (abstrakt)
   - `toDefault()` / `isDefault()` – auf click-TT-Standard zurücksetzen bzw. Vergleich (abstrakt)
   - `CheckData(): boolean` – Validierung (Default `True`)
   - `OnChange: TNotifyEvent` + `FireOnChange` – meldet Änderungen an den Host (für Button-Enable)
   - `Caption` (Formular-Caption) = Seitentitel in Navigation bzw. Fenstertitel
   - `TDialogPanelClass = class of TDialogPanel` – Klassenreferenz als „Seitenfabrik".
2. **Hosts:**
   - `TDialogForMultiplePanel` (diese Unit): TreeView + wechselnde Seite.
   - `TDialogForSinglePanel`: genau eine Seite (z.B. im Erststart-Assistenten).
   - `TDialogPanelMultiTeams` (selbst ein `TDialogPanel`): **zweite Ebene** – links Mannschaftsliste (TreeView), rechts ein `TDialogTeamPanel` (erweitert um `TeamName`) je Mannschaft; Unterklassen liefern `getDialogClass`, `getCaption`, `getHeaderText`. Beispiele: `DialogPanelHomeDays` → `DialogPanelHomeDaysDetail`.
3. **Einzel-Editoren** (modale Kleindialoge, z.B. `DialogEditOneHomeDay`, `DialogEditOneGame`, `DialogEditOneAuswaertsKoppel`, `DialogEditSisterTeam`, `DialogEditTeamName`) werden aus Detail-Panels aufgerufen, Muster `SetValue(...)` → `ShowModal` → bei `mrOk` `GetValue(...)` → `FireOnChange`.

### Lebenszyklus im Multi-Host
- `FormCreate`: `FixFormSize(Self, 1200, 800)` (DPI-skaliert, max. 90 % Bildschirm), leere `PlanData`, `FixControls`.
- `SetValues(SourcePlan, PlanDataDefault, PanelClasses[])`:
  - `PlanData.Assign(SourcePlan)` (tiefe Kopie), Default merken.
  - Für jede Klasse: **temporäre Instanz nur zum Lesen der `Caption`**, TreeView-Knoten mit `Node.Data := PanelClass`, Instanz sofort wieder freigeben. Erster Knoten wird selektiert → löst `OnChange` → `ChangePanel` aus.
- `TreeViewChanging`: Wechsel nur erlaubt, wenn `DialogPanel.CheckData` true (Validierung blockiert Navigation).
- `TreeViewChange` → `ChangePanel(Klasse)`: altes Panel `FormToData` (Änderungen in Arbeitskopie sichern) + `FreeAndNil`; neues Panel erzeugen, `setPanel(PanelMain)`, `setPlanData`, `DataToForm`, `OnChange` verbinden, `EnableButtons`. Guard `csDestroying` gegen Events beim Schließen.
- `ButtonOKClick`: `CheckData` → `FormToData` → `mrOk`. Aufrufer: `PlanData.Assign(Form.getPlanData())`, `DoOnAfterPlanChanged`.
- `ButtonToStandardClick`: `DialogPanel.toDefault` (nur aktuelle Seite!), `EnableButtons`.
- `EnableButtons`: „Standard wiederherstellen" nur sichtbar, wenn `PlanDataDefault` existiert (also bei click-TT-Import), aktiviert wenn Seite nicht Default ist.
- Abbrechen → `mrCancel`, Arbeitskopie wird in `FormDestroy` verworfen.

### Seitenliste im Hauptprogramm (Reihenfolge)
`TDialogPanelMainData, TDialogPanelTeams, TDialogPanelRanking, TDialogPanelLocationsCompact, TDialogPanelHomeCoupleCompact, TDialogPanelAuswaertskoppel, TDialogPanelHomeDays, TDialogPanelSisterTeams, TDialogPanel60km, TDialogPanelFreeDays, TDialogPanelMandatoryDays, TDialogPanelPredefinedGames, TDialogPanelHomeRight`. Der Optimierer wird während des Dialogs pausiert (`Optimizer.Paused := True`).

## Inhalt / Struktur
- Felder: `DialogClasses: TList<TDialogPanelClass>` (gefüllt, aber nie gelesen), `PlanData`, `PlanDataDefault`, `DialogPanel` (aktive Seite).
- Methoden: s.o. (`SetValues`, `getPlanData`, `ChangePanel`, `EnableButtons`, `TreeViewChange/Changing`, Button-Handler).
- UI (.dfm, Caption „Spielplandaten bearbeiten...", `bsSizeToolWin`): `TreeView` (alLeft, ReadOnly, RowSelect, ohne Linien/Root), `PanelMain` (alClient, Host), `PanelBottom` mit „Standard wiederherstellen" (links), OK, Abbrechen.

## Fachliche Logik / Regeln
Keine eigene; reine Orchestrierung. Fachregeln stecken in den Panels.

## Daten & Persistenz
Keine direkte. Arbeitet auf In-Memory-Kopie `TPlanData`; Persistenz macht der Aufrufer (Diff-/Modifikationsdatei).

## Threading / Performance
Keine eigenen Threads; Aufrufer pausiert den Optimizer-Thread während des modalen Dialogs. `TPlanData.Assign` (tiefe Kopie) bei Öffnen und bei OK.

## Plattformabhängigkeiten
VCL: Reparenting eines Panels aus einem fremden `TForm` (VCL-spezifischer Trick), `TTreeView`, `FixFormSize` mit `Screen.PixelsPerInch`.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Dialogs`:
  - `interface IEditorPage { string Title; void Load(PlanDataModel work, PlanDataModel? defaults); void Save(); bool Validate(); void ResetToDefault(); bool IsDefault { get; } event EventHandler Changed; }` bzw. abstrakte Basisklasse `EditorPageViewModel : ObservableObject` (CommunityToolkit.Mvvm).
  - `MultiPageEditorViewModel` mit `ObservableCollection<PageDescriptor>` (Titel + `Func<EditorPageViewModel>` Fabrik), `SelectedPage`, Commands `Ok`, `Cancel`, `ResetToDefault` (CanExecute = `Defaults != null && !Current.IsDefault`).
  - View: `SplitView`/`ListBox` links + `ContentControl` mit DataTemplates (ViewLocator) rechts – kein Reparenting nötig.
  - `SingleEditorViewModel` = Sonderfall mit genau einer Seite (gleiche Basis).
  - `TeamPagedEditorViewModel<TDetail>` für die `TDialogPanelMultiTeams`-Ebene.
- Titel statisch (z.B. `static abstract string Title` oder Attribut) statt temporärer Instanz.
- Navigation-Guard: `SelectedPage`-Setter prüft `Current.Validate()` und verwirft Wechsel (UI-Rückspringen im ListBox explizit behandeln).
- Arbeitskopie: `PlanDataModel.Clone()` (deep copy) – bei OK zurückgeben; Undo/Cancel = Kopie verwerfen.
- Optimierer-Pause muss als Service erhalten bleiben (`IOptimizerControl.Pause()`/`Resume()`).
- Aufwandsschätzung: **M** – Framework einmalig sauber bauen; die Seiten selbst separat.
- Auffälligkeiten: `DialogClasses` ungenutzt; „Standard wiederherstellen" wirkt nur auf aktuelle Seite (Beschriftung suggeriert ggf. mehr); die leere Seitenliste würde `DialogPanel=nil` bei OK → Access Violation (in der Praxis nie leer).

## Offene Fragen
- Soll „Standard wiederherstellen" seitenweise bleiben oder zusätzlich global angeboten werden?
- Nach Schließen wird der Optimizer im Original wieder fortgesetzt (Rest von `ToolButtonDataClick`, nicht Teil dieser Unit) – beim Port prüfen.
