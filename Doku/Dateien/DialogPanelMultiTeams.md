# DialogPanelMultiTeams (DialogPanelMultiTeams.pas + DialogPanelMultiTeams.dfm)

**Kategorie:** UI-Panel (eingebettet) – abstrakter Container (Master/Detail)
**Umfang:** 214 Zeilen .pas / 102 Zeilen .dfm
**Abhängigkeiten (uses):** DialogPanel, DialogTeamPanel, PlanDataObjects (`TPlanData.getTeamNames`), PlanUtils (`FixControls`), PlanTypes; VCL: TTreeView, TPanel, TLabel
**Verwendet von (Subklassen):** DialogPanelSisterTeams, DialogPanel60km, DialogPanelAuswaertskoppel, DialogPanelHomeCoupleCompact, DialogPanelHomeDays, DialogPanelHomeRight, DialogPanelLocationsCompact

## Zweck
Generischer **Master-Detail-Container** für alle Einstellungsseiten, die pro Mannschaft gepflegt werden. Links eine Liste (TreeView ohne Hierarchie) aller Mannschaften der Liga, rechts das Detail-Panel (`TDialogTeamPanel`) der gewählten Mannschaft. Die konkrete Seite (z. B. „Spiele der Nachbarmannschaften") ist eine Subklasse, die nur drei abstrakte Methoden überschreibt (Template-Method-Muster). Selbst ein `TDialogPanel`, lässt sich also in `TDialogForMultiplePanel`/`TDialogForSinglePanel` einbetten (Composite: Panel im Panel).

## Inhalt / Struktur
- `TDialogPanelMultiTeams = class(TDialogPanel)`
  - Abstrakt (von Subklassen zu liefern):
    - `getDialogClass(): TDialogTeamPanelClass` – Klasse des Detail-Panels
    - `getCaption(): String` – Titel (Form-Caption → erscheint im Host-TreeView; auch Header rechts)
    - `getHeaderText(): String` – fester Hinweistext über dem Detail; leer → Hinweis-Panel ausgeblendet
  - privat: `DialogPanel: TDialogTeamPanel` (aktuelles Detail), `ChangePanel(TeamName)`, `DoOnDialogDetailChanged` (reicht `OnChange` des Details nach oben durch)
  - Unit-globale Variable `LastTeam: String` – merkt sich die zuletzt gewählte Mannschaft **über Instanzen und Seiten hinweg** (programmweit).
- Methoden:
  - `FormCreate` → setzt `Caption`/`LabelHeaderText` aus den abstrakten Methoden, blendet `PanelFixedHeaderText` aus, wenn leer; `FixControls`.
  - `DataToForm` → füllt TreeView mit `PlanData.getTeamNames` (alphabetisch sortiert), selektiert `LastTeam` sonst die erste Mannschaft; Selektion löst über `OnChange` `ChangePanel` aus.
  - `ChangePanel(TeamName)` → `LastTeam := TeamName`; altes Detail: `FormToData` + `FreeAndNil`; neues Detail per `getDialogClass.Create(Self)`, `setPanel(PanelDetail)`, `setPlanData(TeamName, PlanData, PlanDataDefault)`, `DataToForm`, `OnChange` verdrahten; Header = `"<Team> / <Caption>"`; `ScrollBoxMain` sichtbar; `FireOnChange`.
  - `TreeViewChanging` → verhindert Mannschaftswechsel, wenn `DialogPanel.CheckData` = False (Validierung vor Wechsel).
  - `TreeViewChange` → `ChangePanel(node.Text)`, außer beim Zerstören (`csDestroying`).
  - `FormToData`, `toDefault`, `isDefault`, `CheckData` → delegieren **nur an das aktuell sichtbare Detail-Panel**.
- UI (.dfm): `PanelMain` (alClient) mit `TreeView` (alLeft, 177 px, ReadOnly, RowSelect, ShowLines/ShowRoot=False, HideSelection=False) und `ScrollBoxMain` (eigentlich ein `TPanel`, initial unsichtbar) mit `PanelHeader` (fett, 15 px), `PanelFixedHeaderText` (AutoSize, BorderWidth 10, `LabelHeaderText`) und `PanelDetail` (alClient; Ziel für das Detail).

## Fachliche Logik / Regeln
Keine eigenen – reine Navigation/Delegation. Die Mannschaftsliste kommt aus `<team teamname=…>`-Knoten.

## Daten & Persistenz
Nur indirekt über das Detail-Panel (`TPlanData`-Baum im Speicher). Keine Dateien.

## Threading / Performance
Keine. Detail-Panels werden bei jedem Wechsel neu erzeugt (Formular + dfm-Streaming) – unkritisch.

## Plattformabhängigkeiten
VCL `TTreeView` inkl. `OnChanging`-Veto, Reparenting (`PanelMain.Parent := …`), `ComponentState/csDestroying`, `FixControls` (Tab-Reihenfolge/Font).

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Panels.MultiTeamPanelViewModel<TDetail>` (abstrakt/generisch) + View mit `ListBox` (Teams) und `ContentControl` (Detail, per DataTemplate).
- `getDialogClass/getCaption/getHeaderText` → abstrakte Properties oder Konstruktorparameter; Subklassen wie `DialogPanelSisterTeams` werden dadurch zu 5-Zeilern oder ganz überflüssig (Registrierung als Tupel `(Caption, HeaderText, Func<TDetail>)`).
- `TreeViewChanging`-Veto → in MVVM: Setter von `SelectedTeam` prüft `CurrentDetail.Validate()` und setzt ggf. zurück (Achtung: UI-Selektion muss zurückgesetzt werden – in Avalonia/WPF bekannter Fallstrick, ggf. via `Dispatcher.Post`).
- `LastTeam` global → statisches Feld in einem UI-State-Service (bewusst beibehalten, da UX-Merkmal).
- Aufwand: **S–M** – kleine Logik, aber zentral für 7 Seiten; einmal sauber generisch lösen.
- **Auffälligkeiten:**
  - `isDefault`/`toDefault` (Z. 139–166) betreffen **nur die aktuell angezeigte Mannschaft**. Der „Standard"-Button im Host setzt also nur diese eine Mannschaft zurück und ist nur für diese aktiv. Für die Migration entscheiden: beibehalten oder „alle Mannschaften".
  - `FormToData` wird beim Wechsel automatisch gerufen; bei Abbruch des Host-Dialogs wird die ganze `PlanData`-Kopie verworfen (Host arbeitet auf Kopie) – korrekt.
  - `DataToForm` gibt die Stringliste frei, ohne try/finally (unkritisch).

## Offene Fragen
- Soll „Auf Standard zurücksetzen" in Mannschaftsseiten weiterhin nur die aktuelle Mannschaft betreffen?
