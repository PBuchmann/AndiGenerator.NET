# AndiGeneratorMain (AndiGeneratorMain.pas + AndiGeneratorMain.dfm)

**Kategorie:** UI-Hauptfenster
**Umfang:** 1173 Zeilen .pas / 840 Zeilen .dfm (davon ca. 540 Zeilen eingebettete ImageList-Bitmapdaten)
**Abhängigkeiten (uses):** Projekt: `PlanTypes`, `PlanUtils`, `PlanPanel`, `PlanOptimizer`, `PlanDataObjects`, `PrintUtil`, `DialogOptions`, `DialogSavePlan`, `DialogProfile`, `DialogQuestionUseUserOptions`, `DialogQuestionHardErrors`, `DialogForSinglePanel`, `DialogForMultiplePanel`, `DialogFirstStart`, `DialogGetUpdates`, dazu alle Datenpanels `DialogPanelRanking`, `DialogPanelFreeDays`, `DialogPanelMandatoryDays`, `DialogPanelMainData`, `DialogPanelTeams`, `DialogPanel60km`, `DialogPanelHomeDays`, `DialogPanelPredefinedGames`, `DialogPanelSisterTeams`, `DialogPanelAuswaertskoppel`, `DialogPanelLocationsCompact`, `DialogPanelHomeCoupleCompact`, `DialogPanelHomeRight`. RTL/VCL/Win: `Windows`, `ShellApi` (ShellExecute, TerminateThread), `System.Win.Registry` (`TRegistryIniFile`), `System.IOUtils` (`TPath`), `Vcl.ComCtrls`/`ToolWin`/`Menus`/`ImgList`, `TTimer`, `TOpenDialog`/`TSaveDialog`.
**Verwendet von:** `AndiGenerator.dpr` (erzeugt das Formular). `PlanPanel.pas` und `DialogFirstStart.pas` nutzen die globalen Funktionen `getMainPlan()`, `DoAfterMainPlanChanged()` und `getCurrentViewedPlan()`.

## Zweck
Das Hauptfenster steuert den gesamten Programmablauf: Laden einer click-TT-XML-Datei (oder einer eigenen „manuellen“ Plan-XML), Zusammenführen mit den Benutzeränderungen (`.modifications`), Laden und Speichern der Optionen, Starten, Pausieren und Beenden des Optimierers, periodisches Abholen des besten Plans (Timer), Verwalten „gemerkter“ Pläne im Plan-Verzeichnis, CSV-Export für click-TT, Drucken, Anleitung (PDF), Updatecheck. Die eigentliche Planansicht ist das eingebettete `TFormPlanPanel` (Unit `PlanPanel`).

## Inhalt / Struktur

### Konstanten
- `cModificationExtension = 'modifications'`: Dateiendung der Diff-Datei.

### Felder von `TFormAndiGeneratorMain`
| Feld | Bedeutung |
|---|---|
| `PlanDataClickTTFile: TPlanData` | unveränderte Basisdaten aus der click-TT-Datei (nur bei click-TT-Dateien gesetzt, sonst `nil`) |
| `PlanData: TPlanData` | aktueller Datenstand = Basisdaten + gemergte Modifikationen (bzw. eigene XML) |
| `PlanLoaded: TPlan` | aus `PlanData` geladener Plan. Enthält die Termine aus `existingschedule` („in Click-TT vorhandener Plan“) und die Optionen. Wird über `getMainPlan()` global bereitgestellt. |
| `PlanOptimized: TPlan` | Anzeigekopie der laufenden Generierung (Termine kommen per Timer aus dem Optimizer) |
| `Optimizer: TPlanOptimizer` | Optimierer-Thread (nil, solange nicht gestartet) |
| `CurrentPlanDirectory: String` | Plan-Verzeichnis (siehe Persistenz) |
| `SavedPlans: TObjectList<TSavedPlan>` | gemerkte Pläne (`TSavedPlan` aus PlanTypes: FileName, Name, FileAge, Valid, Plan) |
| `PlanPanel: TFormPlanPanel` | eingebettete Planansicht (in `PanelPlan`) |
| `FormOptions: TFormOptions` | nicht-modales Optionsfenster, wird lazy erzeugt |
| `LastKosten`, `LastUpdateTick`, `LastTimerTick`, `LastSucessTick`, `LastDurchLaufe` | Zustand für die Timer-Anzeige |
| `PlanIsLoaded`, `currentLoadedFileName`, `currentLoadedFileDate` | `currentLoadedFileDate` wird gesetzt, aber nie gelesen |
| `SearchUpdateThread: TUpdateSearchThread` | Hintergrund-Updatecheck (aus `DialogGetUpdates`) |

### UI-Elemente (.dfm)
- Formular: Titel „Andi-Generator“ plus Dateiversion, `WindowState = wsMaximized`, Schrift MS Sans Serif 8, `OnMouseWheel` scrollt die nächste `TScrollBox` unter der Maus (8 × Increment).
- `PanelTop` (65 px) mit `ToolBarMain` (Text- und Bild-Buttons, `ImageListToolbar`):
  - **„Terminwünsche laden...“** (`ToolButtonLoad`, DropdownMenu `PopupMenuOpen`):
    - „Datei öffnen (XML-Datei von Click-TT) ...“ → `MenuOpenClick`
    - „Neue Datei zur komplett manuellen Eingabe erstellen...“ → `MenuNewClick`
  - **„Generierung starten“** / „... pausieren“ / „... fortsetzen“ (`ToolButtonStartPause`, ImageIndex 7 = Start, 6 = Pause)
  - **„Für Click-TT exportieren...“** (`ToolButtonExport`)
  - **„Plandaten...“** (`ToolButtonData`)
  - **„Einstellungen...“** (`ToolButtonOptions`)
  - **„Sonstiges“** (`ToolButtonDivers`, `PopupMenuDivers`): „Drucken...“, „Anleitung...“, „Auf Updates prüfen...“
- `LabelPlanName` („Kein Plan geladen“), `LabelDurchlauf` (Fortschritt), `LabelHitTest` (nur mit `CACHE_HIT_TEST`), `ButtonProfile` (nur mit `PROFILE`).
- `LabelPlanSelector` „Angezeigter Plan:“ und `ComboBoxPlanSelector`: Index 0 = „laufende Generierung“ (`PlanOptimized`), Index 1 = „in Click-TT vorhandener Plan“ (`PlanLoaded`), ab Index 2 = gemerkte Pläne (Objects[] = `TPlan`).
- `ToolBarViewPlans`: `ButtonRemove` (Hint „Gemerkten Plan entfernen...“, aktiv nur bei Index > 1) und `ButtonSave` („Plan merken...“, aktiv nur bei Index = 0).
- `ComboBoxZoom`: 25 % … 200 % (Standard 100 %) → `PlanPanel.SetZoom(n/100)`.
- `PanelPlan` (alClient): Host für `TFormPlanPanel`.
- Dialoge: `dlgOpen` (Filter *.xml), `dlgNew` (Save, *.xml), `SaveDialog` (*.csv, Überschreib-Nachfrage), `SaveDialogXML` (*.xml, wird im Code nicht genutzt).
- Timer: `TimerSyncPlans` (Interval **777 ms**), `TimerSearchUpdate` (Standard-Interval 1000 ms, von Anfang an aktiv).

### Methoden (Programmablauf)
- **`FormCreate`**: legt `SavedPlans`, `PlanData`, `PlanLoaded` und `PlanOptimized` an, erzeugt `PlanPanel`, hängt die Version an den Titel, ruft `EnableButtons` und `FixControls(Self)` (automatische Tab-Reihenfolge) auf. **Updatecheck:** Aus der Registry `HKCU\Software\Andreas Hofmann\Andi-Generator`, Abschnitt `update`, Wert `lastSearch` (Datum). Liegt die letzte Suche mehr als **2 Tage** zurück, wird `TUpdateSearchThread` gestartet und `lastSearch := now` geschrieben.
- **`TimerSearchUpdateTimer`**: Sobald der Update-Thread fertig ist und kein modaler Dialog offen ist (`Application.ModalLevel = 0`), öffnet sich bei `HasUpdates` der Dialog `TFormGetUpdates`. Danach werden Thread und Timer beendet.
- **`MenuOpenClick`** → `OpenFile(FileName, WithLoadMessages=True)`.
- **`MenuNewClick`**: Legt über `dlgNew` eine leere Plan-XML an (`TPlanData.Create.SaveToXML` → `<andigenerator><plan/></andigenerator>`), ruft `OpenFile(..., False)` auf und öffnet sofort den Plandaten-Dialog (`ToolButtonDataClick`).
- **`OpenFile(FileName, WithLoadMessages)`**, der zentrale Ladevorgang:
  1. `currentLoadedFileName` setzen, Wartecursor, `KillOptimizer`, `SavedPlans.Clear`.
  2. `IsClickTTFile` (Root-Element `TT`):
     - **click-TT:** `PlanDataClickTTFile.LoadFromClickTTFile`, danach `PlanData.Assign(PlanDataClickTTFile)`. Existiert `<Datei>.modifications` (gleicher Pfad, Endung ersetzt), wird sie per `LoadFromXML` geladen und mit `PlanData.Merge(Diff)` übernommen. Fehlt sie, gilt das als **FirstStart**.
     - **sonst:** `PlanData.LoadFromXML(FileName)` (eigenes Format, `PlanDataClickTTFile = nil`).
  3. Debug (`{$ifopt D+}`): `PlanData.SaveToXML('D:\temp\andigenerator.xml')` (fest verdrahteter Pfad!).
  4. `PlanLoaded.Load(PlanData)`, `CurrentPlanDirectory` ermitteln, Optionen aus `<PlanDir>\AndiGenerator.options` laden.
  5. Bei `WithLoadMessages`: Weichen die Optionen vom Standard ab (`getDiffToDefault`), fragt `DialogUseOptionsQuestion`. Bei „Nein“ werden die Optionen zurückgesetzt und gespeichert.
  6. Bei `WithLoadMessages` Heuristiken zur Runde:
     - Ist `|DateBegin − DateEnd| < 6*30 = 180 Tage` und `RoundPlaning <> rpHalfRound`, lautet die Frage „Die Runde ist kürzer als 6 Monate. Soll nur eine Halbrunde generiert werden?“ → `rpHalfRound`.
     - sonst, wenn `|now − DateBeginRueckrundeInXML| < 60 Tage`:
       - Ist das Jahr von `DateBegin` **2020** und `RoundPlaning` weder `rpCorona` noch `rpSecondOnly`: Frage nach „Rückrunde mit den Coronaspezialitäten“ → `rpCorona`.
       - Danach, wenn weiterhin weder `rpCorona` noch `rpSecondOnly`: Frage „Soll nur die Rückrunde generiert werden?“ → `rpSecondOnly`.
     - Jede Änderung wird sofort in die Optionsdatei gespeichert.
  7. `WithLoadMessages and FirstStart` → `TDialogFirstStart` (modal).
  8. `PlanOptimized.Assign(PlanLoaded)`, `clearAllDates`, `RemoveNotValidDates` → der Optimierer startet ohne Termine.
  9. `PlanIsLoaded := true`, `InitPlanMainData`, Plan-Panel auf Seite 0, Selector auf Index 1 („in Click-TT vorhandener Plan“).
- **`InitPlanMainData`**: Plan-Verzeichnis neu bestimmen, Planname im Label, `FillPlanSelector`, `SyncSavedPlans(False)`, alten Selector-Index wiederherstellen, `FormOptions.SetValues`, Optionen von `PlanOptimized` an alle gültigen SavedPlans übertragen, `EnableButtons`.
- **`GetCurrentPlanDirectory(PlanData)`**: `getAppLocalPath() + '\Andi-Generator\' + EncodeFileName(<name> + ' ' + <Jahr von from>)`, dann `ForceDirectories`. Bei einer Exception wird `''` zurückgegeben.
- **`ToolButtonStartPauseClick`**:
  - Läuft noch kein Optimizer: `TPlanOptimizer.Create`, `Optimizer.Plan.Assign(PlanOptimized)`, `RemoveNotValidDates`, `Start`. Anzeige-Reset (`LastKosten := -1` usw.), PlanPanel auf Seite 2, Selector auf 0 („laufende Generierung“).
  - Läuft er schon: `Optimizer.Paused := not Paused` und Button-Beschriftung umschalten.
  - Ein „Stop“ gibt es nicht. Der Optimizer wird nur über `KillOptimizer` beendet (beim Öffnen einer Datei und beim Schließen).
- **`TimerSyncPlansTimer`** (alle 777 ms, Timer während der Ausführung deaktiviert):
  1. `SyncSavedPlans(False)`: Das Plan-Verzeichnis wird **bei jedem Tick** gescannt.
  2. Wenn der Optimizer existiert und nicht pausiert: `Optimizer.copyPlanDates(PlanOptimized)` (unter der CriticalSection des Optimizers), `Durchlaufe` lesen, `PlanPerSec` → Label „N Pläne wurden berechnet (X Pläne/s)“. Liegt die letzte Verbesserung mehr als 10 s zurück, kommt „, letzte Verbesserung vor: M Min S Sek“ dazu. Mit TRACE wird zusätzlich der Thread-Name der letzten Verbesserung angezeigt.
  3. Haben sich die Kosten von `PlanOptimized` geändert oder sind mehr als 10 s vergangen: `PlanPanel.SetPlan(GetCurrentViewPlan)` (Neuaufbau der Ansicht). Bei geänderten Kosten wird `LastSucessTick` gesetzt.
- **`ToolButtonDataClick`**: Optimizer pausieren. `TDialogForMultiplePanel` mit `PlanData` und `PlanDataClickTTFile` und den Panels `MainData, Teams, Ranking, LocationsCompact, HomeCoupleCompact, Auswaertskoppel, HomeDays, SisterTeams, 60km, FreeDays, MandatoryDays, PredefinedGames, HomeRight`. Bei OK: `PlanData.Assign(Form.getPlanData)` und `DoOnAfterPlanChanged(nil, false)`. Danach den vorherigen Pausenzustand wiederherstellen.
- **`DoOnAfterPlanChanged(Sender, SaveOnlyOptions)`**, aufgerufen nach dem Speichern der Optionen (`FormOptions.OnAfterSave`), nach dem Plandaten-Dialog und aus `PlanPanel` über `DoAfterMainPlanChanged(True)`:
  1. Optionen speichern.
  2. Wenn nicht `SaveOnlyOptions`: `SavePlan` (Diff bzw. Voll-XML).
  3. `PlanLoaded.Load(PlanData)` (hier geht der Optionsstand nicht verloren, weil vorher gespeichert wurde. Ob `Load` die Optionen neu lädt, ist in PlanTypes zu prüfen), `PlanOptimized.AssignPlanData(PlanLoaded)` und `RemoveNotValidDates` (Termine bleiben erhalten).
  4. `InitPlanMainData` und erneut Optionen speichern.
  5. `AssignPlanData` in alle gültigen SavedPlans. Wenn nicht `SaveOnlyOptions`: `SyncSavedPlans(True)` (alle neu laden).
  6. `Optimizer.setNewPlanData(PlanOptimized)` → alle Inseln und Worker werden mit den neuen Daten und Optionen re-initialisiert, die Termine bleiben (soweit gültig) erhalten.
  7. Ansicht aktualisieren, `LastKosten` neu berechnen, `FormOptions.SetValues`.
- **`SavePlan`**: Ohne `PlanDataClickTTFile` (manueller Plan) wird `PlanData.SaveToXML(currentLoadedFileName)` aufgerufen und **die Originaldatei überschrieben**. Mit click-TT-Basis: `DiffData.Diff(PlanData, PlanDataClickTTFile)` → `<Datei>.modifications`.
- **`ButtonSaveClick`** („Plan merken“): Optimizer pausieren. `ShowDialogSavePlan(CurrentPlanDirectory, SavedPlans, S)` liefert den Namen. Datei: `CurrentPlanDirectory + '\' + EncodeFileName(S) + '.xml'` → `PlanOptimized.SaveScheduleToXML`. Danach `SyncSavedPlans`. (`while true … break` ist eine Schleife ohne Wiederholung.)
- **`ButtonRemoveClick`**: Nach Bestätigung „"<Name>" löschen?“ wird die Datei des aktuell angezeigten gemerkten Plans gelöscht (`DeleteFile`) und `SyncSavedPlans` aufgerufen.
- **`SyncSavedPlans(ReloadAll)`**: `ReloadAll` setzt alle `FileAge := 0`. Das Verzeichnis wird mit `*.xml` gescannt, entfernte Dateien werden aus der Liste genommen, neue oder geänderte (FileAge-Vergleich) per `LoadSavePlan` geladen. Bei Änderungen: absteigend nach FileAge sortieren (neueste zuerst), `FillPlanSelector`, Ansicht aktualisieren.
- **`LoadSavePlan`**: Name = `DecodeFileName(Dateiname ohne Endung)`. `Plan.Assign(PlanLoaded)` + `LoadScheduleFromXml(FileName)`. Bei einer Exception gilt `Valid := False`, der Plan wird dann im Selector nicht angezeigt.
- **`FillPlanSelector`**: Die zwei festen Einträge plus die gültigen SavedPlans. Die Auswahl bleibt über den Text erhalten, sonst über `min(OldIndex, Count−1)`.
- **`ToolButtonExportClick`**: Kopie des angezeigten Plans. `getHardErrorMessages` → bei harten Fehlern Rückfrage `DialogHardErrors`. SaveDialog mit Vorschlag `MakeValidFileName(PlanName) + '.csv'` → `TPlan.SaveScheduleToCsv` (Format siehe PlanTypes; Header `Nr.;Vor/Rück;Tag;;Datum;;Uhrzeit;HeimVereinNr;Heim-Mannschaft;GastVereinNr;Gast-Mannschaft;HeimMannschaftNr;GastMannschaftNr;Ergebnisse;Spiellokal`).
- **`MenuPrintClick`**: Pausieren → `PlanPanel.Print()` → Pausenzustand wiederherstellen.
- **`Anleitung1Click`**: `ExtractFilePath(ExeName) + '\Andigenerator.pdf'` (doppelter Backslash, weil `ExtractFilePath` bereits mit `\` endet) → `ShellExecute`. Rückgabewert < 32 → Meldung „evtl. ist der Acrobat Reader nicht installiert!“.
- **`AufUpdatesprfen1Click`**: `TFormGetUpdates` modal.
- **`ButtonProfileClick`** (nur PROFILE): pausieren → `DialogProfile.ShowDialogProfile(Kopie)`.
- **`KillOptimizer`**: `Terminate`, Busy-Wait `while not Finished do Sleep(2)`, `FreeAndNil`, Button auf „Generierung starten“.
- **`FormDestroy`**: `PlanLoaded` und `PlanOptimized` freigeben, **danach** `KillOptimizer` (der Optimizer hat eigene Kopien, deshalb unkritisch). Läuft der Update-Thread noch, wird er mit **`TerminateThread`** hart beendet.
- **`EnableButtons`**: Start/Export/Print/Options/Data nur bei geladenem Plan. Remove bei Index > 1, Save bei Index = 0.
- Globale Funktionen: `getMainPlan()` = `PlanLoaded`, `DoAfterMainPlanChanged(SaveOnlyOptions)`, `getCurrentViewedPlan()`.

## Fachliche Logik / Regeln
- Halbrunden-Heuristik: Rundendauer unter 180 Tagen → Vorschlag `rpHalfRound`.
- Rückrunden-Heuristik: Der Rückrundenbeginn liegt weniger als 60 Tage von „heute“ entfernt → Vorschlag `rpSecondOnly`. Bei Saisonbeginn im Jahr 2020 zusätzlich der Vorschlag `rpCorona` (fest einkodiertes Jahr).
- Unterscheidung „in Click-TT vorhandener Plan“ (`existingschedule` aus der XML) vs. „laufende Generierung“ vs. gemerkte Pläne.
- Vor dem Export werden harte Fehler geprüft und der Benutzer gefragt.

## Daten & Persistenz
| Was | Ort / Format |
|---|---|
| click-TT-Datei (Eingabe) | beliebiger Pfad, XML mit Root `TT` (siehe PlanDataObjects.md) |
| Benutzeränderungen | `<click-TT-Datei ohne Endung>.modifications` im **selben Verzeichnis wie die click-TT-Datei**, XML `<andigenerator><plan …>` mit `state`-Attributen (Diff) |
| manueller Plan („Neu“) | vom Benutzer gewählte `.xml`, Voll-Format `<andigenerator><plan …>`, wird bei jeder Änderung überschrieben |
| Plan-Verzeichnis | `%LOCALAPPDATA%\Andi-Generator\<EncodeFileName("<Staffelname> <Jahr(from)>")>\` |
| Optionen | `<Plan-Verzeichnis>\AndiGenerator.options` (XML `andigenerator-options`, Encoding iso-8859-15, siehe PlanTypes `TCalculateOptions`) |
| gemerkte Pläne | `<Plan-Verzeichnis>\<EncodeFileName(Name)>.xml`, XML `<andigenerator><plan><schedule><game datetime hometeamname guestteamname/>…` |
| CSV-Export | vom Benutzer gewählt, `.csv` (Semikolon) |
| Anleitung | `<Exe-Verzeichnis>\Andigenerator.pdf` |
| Registry | `HKCU\Software\Andreas Hofmann\Andi-Generator\update\lastSearch` (`TRegistryIniFile.WriteDate` → binärer Double-Wert) |
| Debug-Dump | `D:\temp\andigenerator.xml` (nur mit Debug-Info D+) |
| Log | `%TEMP%\Andi-Generator.log` (PlanUtils, nur mit TRACE) |

## Threading / Performance
- UI-Thread: Der Timer (777 ms) holt den besten Plan per `copyPlanDates` (sperrt `CriticalSectionOptimizer`). Das Warten auf den Lock kann die UI kurz blockieren, weil der Optimizer den Lock ca. 1×/s hält und dabei die Insel-Locks nimmt.
- `Optimizer.Durchlaufe` (Int64) wird ohne Lock gelesen. Unter Win32 ist ein zerrissener Lesewert möglich (nur Anzeige).
- `SyncSavedPlans` scannt bei jedem Tick das Verzeichnis (Datei-IO im UI-Thread).
- `PlanPanel.SetPlan` baut die gesamte Ansicht höchstens bei Kostenänderung bzw. alle 10 s neu auf.
- Pause/Resume-Muster: Jeder modale Dialog pausiert den Optimizer (`WasPaused`-Muster, 6× dupliziert).
- `StartWaitCursor()` wird ohne Zuweisung aufgerufen: Das zurückgegebene Interface lebt als implizite Variable bis zum Ende der Methode (RAII-Scope-Guard).

## Plattformabhängigkeiten
- VCL-Formular, TToolBar/TToolButton mit DropdownMenu, TImageList (Bitmaps im .dfm), TTimer, Common Dialogs, `FindVCLWindow`/TScrollBox-Mausrad.
- Registry (`TRegistryIniFile`).
- `ShellExecute` (PDF öffnen), `TerminateThread`.
- `CSIDL_LOCAL_APPDATA` (über `getAppLocalPath`), fest verdrahtete `'\'`-Pfadtrenner, `'D:\temp\…'`.
- `GetTickCount` (32 Bit, Überlauf nach 49,7 Tagen).
- `Application.ModalLevel`.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI` → `MainWindow` (View, z.B. Avalonia) + `MainViewModel` (MVVM, CommunityToolkit.Mvvm). Die Ablauflogik (OpenFile, SavePlan, SyncSavedPlans, Pfade) gehört in einen UI-freien Service `AndiGenerator.Core.PlanSession` bzw. `ProjectService`, damit sie testbar ist und auch mobil funktioniert.
- Pfade: `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` + `Path.Combine` (kein `'\'`). Unter macOS/Linux ergibt das `~/.local/share` bzw. `~/Library/Application Support`. Das bestehende Verzeichnis `Andi-Generator` beibehalten, damit Windows-Nutzer ihre gemerkten Pläne und Optionen behalten.
- Registry → JSON-Settings-Datei (`settings.json` im AppData-Verzeichnis) für `update.lastSearch`.
- Timer → `DispatcherTimer` (Avalonia) oder `PeriodicTimer` + `Dispatcher.UIThread.Post`. Besser: Der Optimizer meldet Verbesserungen per Event oder `Channel` (Push statt Poll). Die Anzeige wird gedrosselt (z.B. max. 1–2 Hz).
- Verzeichnis-Scan → `FileSystemWatcher`, alternativ Scan in einem Hintergrund-Task mit geringerer Frequenz.
- `ShellExecute` → `Process.Start(new ProcessStartInfo(path){UseShellExecute=true})` bzw. `Launcher.LaunchFileAsync` (Avalonia `TopLevel.Launcher`). Auf Mobilgeräten die PDF einbetten oder per URL öffnen.
- `TerminateThread` → kooperative Abbrüche mit `CancellationToken` + `HttpClient`-Timeout.
- `KillOptimizer`-Busy-Wait → `await optimizer.StopAsync()` (CancellationToken + `Task.WhenAll`).
- Pause-Muster → `using var _ = optimizer.PauseScope();` (IDisposable, das den vorherigen Zustand wiederherstellt).
- Die Heuristik mit „Corona 2020“ ist ein fest einkodiertes Jahr. Übernehmen oder entfernen? (siehe Offene Fragen)
- Dialog-Mapping: `TDialogForMultiplePanel` mit einer Liste von Panel-Klassen → `TabControl`/Wizard mit `IEnumerable<PanelViewModel>`.
- Dateidialoge → `IStorageProvider` (Avalonia). Auf Android/iOS gibt es nur Streams, keine Pfade. Das berührt das Konzept „`.modifications` neben der click-TT-Datei“: Auf Mobilgeräten muss die Modifikationsdatei im App-Verzeichnis liegen (Schlüssel z.B. Staffel-`id` + Jahr).
- Locale: MyFormatInt/-Float nutzen die Windows-Ländereinstellung (Tausenderpunkt). In C# explizit `CultureInfo("de-DE")`.
- Aufwand: **L**. Der ganze Ablauf, der Dateiumgang und die Mobile-Tauglichkeit von Dateipfaden erfordern Umbau. Dazu kommt MVVM-Neuentwurf statt 1:1-Port.

### Bugs/Auffälligkeiten
- Z. 780: Fest verdrahteter Debug-Pfad `D:\temp\andigenerator.xml` (Exception, wenn das Verzeichnis fehlt; nur mit D+).
- Z. 243: `ExtractFilePath(...) + '\Andigenerator.pdf'` erzeugt einen doppelten Backslash (unter Windows harmlos).
- Z. 404: Bei manuellen Plänen wird die Originaldatei ohne Rückfrage überschrieben.
- Z. 213: `TerminateThread` auf den Update-Thread (kann Ressourcen oder Locks hinterlassen).
- Z. 748: `currentLoadedFileDate` wird nie gelesen (toter Code).
- Z. 822: Das Jahr 2020 ist fest kodiert.
- Z. 924–931: Hat die Plandatei kein `from`-Datum (neuer manueller Plan), liefert `GetAsDate` 0 → Jahr 1899 → Verzeichnis „<Name> 1899“ bzw. „ 1899“ für leeren Namen.
- `SaveDialogXML` ist deklariert, aber ungenutzt.
- Timer-Tick: `SyncSavedPlans` bei jedem Tick (IO-Last). `GetTickCount`-Überlauf wird nicht behandelt.

## Offene Fragen
- Soll die `.modifications`-Datei weiterhin neben der click-TT-Datei liegen (braucht Schreibrecht im Download-Verzeichnis), oder soll sie ins App-Datenverzeichnis wandern (Pflicht für Mobilgeräte)?
- Soll die Corona-2020-Abfrage entfallen?
- Soll das Anleitungs-PDF mitgeliefert werden (Pfad neben der EXE) oder online verlinkt werden?
- Updatecheck: Welche URL bzw. welcher Mechanismus (siehe `DialogGetUpdates`)? Auf Mobilgeräten übernehmen das die Stores.
- Muss das Optionsformat `AndiGenerator.options` (iso-8859-15) kompatibel bleiben, damit bestehende Nutzerdaten weiter funktionieren?
