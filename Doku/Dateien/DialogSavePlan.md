# DialogSavePlan (DialogSavePlan.pas + DialogSavePlan.dfm)

**Kategorie:** UI-Dialog (modal) / Persistenz-nah
**Umfang:** 150 Zeilen .pas / 116 Zeilen .dfm
**Abhängigkeiten (uses):** PlanTypes (`TSavedPlan`), PlanUtils (`EncodeFileName`, `FixControls`), Winapi.ShellAPI (`ShellExecute`), System.SysUtils (`DeleteFile`); VCL: TListView, TPopupMenu, `MessageDlg`
**Verwendet von:** AndiGeneratorMain (`SavePlan`, ca. Z. 335–370)

## Zweck
Dialog „**Plan merken...**": Der Benutzer vergibt einen Namen, unter dem der aktuell optimierte Plan als Zwischenstand gespeichert wird. Zeigt die bereits gemerkten Pläne, erlaubt Überschreiben (mit Rückfrage), Löschen (Kontextmenü) und das Öffnen des Speicherverzeichnisses im Explorer.

## Inhalt / Struktur
- `function ShowDialogSavePlan(CurrentPlanDirectory; SavedPlans: TObjectList<TSavedPlan>; var ResultValue: String): Boolean` – Fassade; bei `mrOk` → `ResultValue := Trim(EditName.Text)`.
- `FillPlans` → `ListPlans.AddItem(SavedPlan.Name)` für alle gemerkten Pläne.
- `PlanExists(Name)` → Vergleich **case-insensitiv** (`AnsiUpperCase`) mit den Listeneinträgen.
- `ButtonOKClick` → leerer Name: „Bitte geben Sie einen Namen ein."; existiert: „"X" existiert bereits, möchten Sie den Plan überschreiben?" (Ja → OK).
- `ListPlansClick` → übernimmt Namen des selektierten Eintrags ins Eingabefeld.
- `Lschen1Click` (Kontextmenü „Löschen...") → Rückfrage „"X" löschen?" → `DeleteFile(CurrentPlanDirectory + '\' + EncodeFileName(Name) + '.xml')` und Eintrag entfernen.
- `LinkLabel1Click` („Gehe zu Speicherverzeichnis...", blau unterstrichen) → `ShellExecute(0, nil, Verzeichnis, …, SW_SHOW)`.
- UI (.dfm): Label „Vorhandene Pläne", `ListPlans` (vsReport, eine Spalte „Name", ohne Header, PopupMenu), Label „Bezeichnung des Plans:", `EditName` (ActiveControl), OK (Default) / Abbruch (Cancel).

## Fachliche Logik / Regeln
- Planname nicht leer (nach Trim); gleichnamige Pläne (Groß-/Kleinschreibung egal) werden nach Bestätigung überschrieben.

## Daten & Persistenz
- Speicherverzeichnis (vom Aufrufer): `%LOCALAPPDATA%\Andi-Generator\<EncodeFileName(Liganame + ' ' + Jahr(Start Vorrunde))>\` (via `SHGetFolderLocation(CSIDL_LOCAL_APPDATA)`).
- Dateiname: `EncodeFileName(Name) + '.xml'` – `EncodeFileName` ersetzt `# < > " [ ] / \ ? * : |` durch `#` + dreistelligen Dezimalcode (z. B. `:` → `#058`). Gespeichert wird vom Aufrufer per `PlanOptimized.SaveScheduleToXML`.
- Im selben Verzeichnis liegt auch `AndiGenerator.options`.
- Die Löschfunktion löscht die Datei **sofort** (vor OK/Abbruch des Dialogs).

## Threading / Performance
Keine. (Aufrufer pausiert den Optimierer während des Dialogs.)

## Plattformabhängigkeiten
- `ShellExecute` (Explorer öffnen) → Cross-Platform: `Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true })` bzw. `xdg-open`/`open`; Avalonia `ILauncher.LaunchDirectoryInfoAsync`.
- Pfad mit `'\'` → `Path.Combine`.
- `%LOCALAPPDATA%` → `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` (Linux `~/.local/share`, macOS `~/Library/Application Support`); Mobil: App-Sandbox.
- Dateinamen-Kodierung: `#`-Escaping muss **identisch** bleiben, damit vorhandene gemerkte Pläne weiter gefunden werden.
- Case-insensitive Namensvergleich vs. case-sensitive Dateisysteme (Linux) – Überschreiben von „Plan" durch „plan" erzeugt unter Linux zwei Dateien.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.UI.Dialogs.SavePlanDialog` + Service `AndiGenerator.Persistence.SavedPlanStore` (List/Save/Delete/GetDirectory), damit Datei-I/O nicht im Dialog liegt.
- `AnsiUpperCase`-Vergleich → `StringComparer.CurrentCultureIgnoreCase`.
- Aufwand: **S**.
- **Bugs/Auffälligkeiten:**
  - Z. 133: `ListPlansClick` greift auf `ListPlans.Selected.Caption` ohne nil-Prüfung zu → Zugriffsverletzung beim Klick auf leeren Bereich der Liste.
  - Z. 143: Löschpfad wird aus dem Anzeigenamen neu kodiert statt `TSavedPlan.FileName` zu verwenden; funktioniert nur, solange `DecodeFileName(EncodeFileName(x)) = x` gilt. Rückgabewert von `DeleteFile` wird ignoriert.
  - Die übergebene `SavedPlans`-Liste wird beim Löschen nicht aktualisiert (Aufrufer ruft danach `SyncSavedPlans` nur beim Speichern).

## Offene Fragen
- Speicherort auf macOS/Linux/Mobil – beibehalten der Struktur „Andi-Generator/<Liga Jahr>"?
