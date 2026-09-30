# DialogGetUpdates (DialogGetUpdates.pas + DialogGetUpdates.dfm)

**Kategorie:** UI-Dialog + Netzwerk/Threading (Update-Prüfung)
**Umfang:** 255 Zeilen .pas / 67 Zeilen .dfm
**Abhängigkeiten (uses):** projekt-eigen: `PlanUtils` (`FixControls`), `PlanTypes`; RTL/Win: `UrlMon` (`URLDownloadToFile`), `System.IniFiles` (`TIniFile`), `ShellApi` (`ShellExecute`), `System.IOUtils` (`TPath`, `TFile`), `Winapi.Windows` (`TerminateThread`, `GetFileVersionInfo*`, `VerQueryValue`); VCL: `Forms`, `StdCtrls`, `ExtCtrls` (`TTimer`)
**Verwendet von:** `AndiGeneratorMain` – (a) Menü „Auf Updates prüfen" (`AufUpdatesprfen1Click`, Z. ~255: Dialog modal öffnen), (b) automatische Hintergrundprüfung beim Programmstart mit eigener `TUpdateSearchThread`-Instanz (`StartTestUpdateThread`, Z. ~184–195; Auswertung in `TimerSearchUpdateTimer`, Z. ~539–560; Abbruch in `FormDestroy`, Z. ~209–215).

## Zweck
Prüft, ob eine neuere Version des AndiGenerators verfügbar ist, indem eine INI-Datei von der Website des Autors heruntergeladen und deren Versionsnummer mit der Dateiversion der laufenden EXE verglichen wird. Zeigt das Ergebnis im Dialog an und bietet einen klickbaren Download-Link.

## Inhalt / Struktur
### `TUpdateSearchThread = class(TThread)`
- Öffentliche Ergebnisfelder: `HasError`, `HasUpdates: boolean`, `Url`, `Version: String` (keine Synchronisierung; Lesen erst nach `Finished`).
- `Create` → `inherited Create(true)` (suspendiert; Start durch Aufrufer mit `.Start`). `FreeOnTerminate` = false (Default).
- `Execute`:
  1. `TempFileName := TPath.GetTempFileName()` (legt leere Temp-Datei an).
  2. `URLDownloadToFile(nil, 'https://sv-schwaig.de/download/generator/updateinfo.ini', TempFileName, 0, nil)` – **synchroner** HTTPS-GET über WinINet/URLMon (IE-Proxy-/Zertifikatseinstellungen, IE-Cache), kein Timeout, keine Abbruchmöglichkeit, kein Terminated-Check.
  3. Bei `S_OK`: `TIniFile` lesen, Sektion `[info]`, Schlüssel `version` und `url` (Default ''); `AppVersion := GetAppVersionStr`; `HasUpdates := IsNewerVersion(AppVersion, Version) < 0`.
  4. Sonst `HasError := true`.
  5. `TFile.Delete(TempFileName)`.
- Erwartetes Format der Server-Datei:
  ```ini
  [info]
  version=<z.B. 1.2.3.4>
  url=<Download-URL>
  ```

### Hilfsfunktionen (unit-privat)
- `GetAppVersionStr` – liest die VERSIONINFO-Ressource der EXE (`GetFileVersionInfoSize/GetFileVersionInfo/VerQueryValue('\')`) → `"Major.Minor.Release.Build"`; wirft bei Fehler `RaiseLastOSError`.
- `IsNewerVersion(Ver1, Ver2): Integer` – segmentweiser Vergleich an `.`; Segment numerisch (`StrToInt`) → Integervergleich, sonst `CompareStr` (ordinal, case-sensitiv). Ergebnis −1/0/1 (bzw. CompareStr-Differenz). Nach der Schleife wird das jeweils nächste Restsegment verglichen. **Hinweis:** Enden Versionen mit unterschiedlicher Segmentanzahl, wird nur bis zum kürzeren + 1 Segment verglichen (z.B. „1.2" vs. „1.2.0.1" → Vergleich „2" vs. „2" → 0 = gleich).

### `TFormGetUpdates = class(TForm)`
- `FormCreate` → `FixControls`, eigenen `TUpdateSearchThread` erzeugen und starten.
- `TimerUpdateTimer` (TTimer, Default-Intervall 1000 ms, Polling): wenn Thread `Finished` → Timer aus; Anzeige:
  - Fehler: „Updateinformation kann nicht abgerufen werden"
  - Update: „Die neue Version <Version> des Andigenerators kann hier runtergeladen werden:" + `LinkLabel.Caption := Url`, sichtbar
  - sonst: „Andigenerator ist aktuell"
- `LinkLabelClick` → `ShellExecute(0, nil, PChar(LinkLabel.Caption), nil, nil, SW_SHOW)` (öffnet Standardbrowser).
- `FormDestroy` → läuft der Thread noch: **`TerminateThread(Handle, 0)`** (harter Abbruch), dann `Free`.
- `ButtonOKClick` → `mrOk`.

UI (.dfm, Caption „Updates", `bsDialog`, 355×114): `Label1` („Updates werden gesucht..."), `LinkLabel` (unsichtbar, blau unterstrichen, Hand-Cursor), OK-Button, `TimerUpdate`.

## Fachliche Logik / Regeln
- Automatische Prüfung (Hauptform): höchstens alle **2 Tage** – `Registry HKCU\Software\Andreas Hofmann\Andi-Generator`, Sektion `update`, Wert `lastSearch` (TDateTime via `TRegistryIniFile.ReadDate/WriteDate`); wenn `lastSearch < now − 2` → Thread starten und `lastSearch := now` sofort schreiben (auch wenn die Prüfung später fehlschlägt).
- Hauptform zeigt den Update-Dialog nur, wenn der Hintergrund-Thread fertig ist **und** `Application.ModalLevel = 0` (kein anderer modaler Dialog offen) **und** `HasUpdates`. Der Dialog startet dann **einen zweiten Download** (eigener Thread), statt das Ergebnis wiederzuverwenden.
- Fehler bleiben bei der automatischen Prüfung stumm.

## Daten & Persistenz
- Netzwerk: HTTPS GET `https://sv-schwaig.de/download/generator/updateinfo.ini` (fest kodiert), Protokoll HTTP(S) über URLMon, kein User-Agent/Header-Handling, keine Signatur-/Integritätsprüfung.
- Temp-Datei im Benutzer-Temp-Verzeichnis (`GetTempFileName`), wird gelöscht (nicht bei Exception).
- INI-Format s.o. (Windows `GetPrivateProfileString` via `TIniFile`, ANSI/UTF-16 je nach BOM).
- Registry (nur Hauptform): `HKCU\Software\Andreas Hofmann\Andi-Generator\update\lastSearch`.

## Threading / Performance
- Ein Worker-Thread pro Prüfung (Start-Check + ggf. Dialog). Ergebnisübergabe über ungeschützte Felder; Sichtbarkeit wird de facto durch `Finished`-Polling im UI-Thread hergestellt (kein Memory-Barrier-Problem auf x86, formal unsauber).
- Polling per `TTimer` statt Event/`Synchronize`.
- `TerminateThread` ist gefährlich (kann Locks im Heap/WinINet halten, Ressourcenlecks) – wird beim Schließen des Dialogs/Programms während laufendem Download aufgerufen.

## Plattformabhängigkeiten
- `URLDownloadToFile` (UrlMon/WinINet), `TIniFile` (Win-Profile-API), `ShellExecute`, `TerminateThread`, `GetFileVersionInfo`/`VerQueryValue` (PE-Versionsressource), Registry (Hauptform) – **alles Windows-only**.

## Migrationshinweise für C#
- Ziel: `AndiGenerator.Core/Services/UpdateService` (`Task<UpdateCheckResult> CheckAsync(CancellationToken)`), `UpdateDialogViewModel` in `.UI`.
- Netzwerk: `HttpClient` (statisch/`IHttpClientFactory`) mit Timeout (z.B. 10 s), `CancellationToken` statt `TerminateThread`; direkt als String laden (kein Temp-File).
- INI-Parsing: kleiner eigener Parser oder `Microsoft.Extensions.Configuration.Ini` (Sektion `info`, Keys `version`, `url`).
- Versionsvergleich: `System.Version.TryParse` auf beiden Seiten; Fallback auf segmentweisen Vergleich, falls Nicht-Zahlen vorkommen. Bewusst entscheiden, ob die Original-Semantik (unterschiedliche Segmentzahl) erhalten bleiben soll.
- Eigene Version: `Assembly.GetEntryAssembly().GetName().Version` bzw. `AssemblyInformationalVersion` (plattformneutral).
- Link öffnen: `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })` bzw. Avalonia `TopLevel.Launcher.LaunchUriAsync` (macOS/Linux-tauglich); URL vorher validieren (nur `https://`).
- „Letzte Prüfung": Einstellungsdatei (z.B. JSON in `Environment.SpecialFolder.ApplicationData/AndiGenerator/settings.json`) statt Registry; Intervall 2 Tage beibehalten.
- Ergebnis der Hintergrundprüfung an den Dialog übergeben statt erneut herunterzuladen; Anzeige nur, wenn kein modaler Dialog offen (Dialog-Service-Queue).
- Mobile (Android/iOS): In-App-Updatecheck ggf. deaktivieren (Store-Richtlinien).
- Aufwandsschätzung: **S** – überschaubar, sauberer mit async/await.
- Bugs/Auffälligkeiten:
  - Z. 70: `TerminateThread` → mögliche Deadlocks/Lecks.
  - Z. 236 (`GetAppVersionStr` wirft) bzw. INI-Fehler im Thread: Exception wird vom TThread-Rahmen verschluckt (`FatalException`), `HasError` bleibt `false` → Dialog meldet fälschlich „Andigenerator ist aktuell"; Temp-Datei bleibt liegen (kein try/finally).
  - Z. 142–216: `IsNewerVersion` behandelt unterschiedliche Segmentanzahl unvollständig; `CompareStr` liefert beliebige Differenzwerte (nur Vorzeichen wird genutzt – ok).
  - Z. 78: URL aus dem Internet wird ungeprüft an `ShellExecute` übergeben (theoretisch beliebiges Kommando/Datei) – Sicherheitsrisiko, in C# auf `https`-Schema beschränken.
  - Keine Integritätsprüfung (nur Link-Anzeige, kein Auto-Download → Risiko gering).
  - Doppelter Download (Start-Check + Dialog).

## Offene Fragen
- Ist die URL `https://sv-schwaig.de/download/generator/updateinfo.ini` weiterhin gültig, und wo soll die C#-Version ihre Update-Info beziehen (z.B. GitHub Releases API)?
- Soll das Versionsschema der C#-App an die Delphi-Versionen anschließen (Vergleichbarkeit in `updateinfo.ini`)?
- Soll der automatische Check abschaltbar sein (Datenschutz, Offline-Betrieb)?
