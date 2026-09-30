# PlanUtils (PlanUtils.pas)

**Kategorie:** Hilfsfunktionen
**Umfang:** 685 Zeilen .pas / keine .dfm
**Abhängigkeiten (uses):** keine projekt-eigenen Units. RTL/VCL/Win: `Forms`, `Windows`, `SysUtils`, `System.DateUtils`, `SyncObjs` (`TCriticalSection`), `Vcl.Controls`, `Vcl.StdCtrls` (`TComboBox`), `ShlObj` (`SHGetFolderLocation`), `ShellAPI`, `ComObj`, `Dialogs`, `Math`.
**Verwendet von:** praktisch allen Units (per grep 39 Units), u.a. `PlanTypes`, `PlanDataObjects`, `PlanMainThread`, `PlanOptimizer`, `PlanPanel`, `PrintUtil`, `AndiGeneratorMain` und alle Dialoge und Panels.

## Zweck
Eine Sammlung projektweiter Hilfsfunktionen: Zahlen- und Datumsformatierung für die Anzeige, Wochen- und Montagsberechnung (im Kostenmodell für „Spiele pro Woche“ verwendet), Float-Parsing mit Punkt, Trace-Logging in eine Temp-Datei, Wartecursor, AppData-Pfad, Kodierung von Dateinamen, Dateiliste, Versionsinfo der EXE, automatische Tab-Reihenfolge und Wine-Erkennung für die Schriftart. Außerdem wird der Typ `MyDouble = Double` definiert, der in den Kosten verwendet wird.

## Inhalt / Struktur
- Konstante `cNewLine = #13#10`.
- Typen `MyDouble = Double`, `PMyDouble`.
- **Formatierung** (alle locale-abhängig über die globalen `FormatSettings`):
  - `MyFormatFloat(v)`: bei `(v < 10) and (v <> 0)` → `'%1.2n'` (2 Nachkommastellen, mit Tausendertrennzeichen), sonst `'%1.0n'`. Negative Werte haben daher immer 2 Nachkommastellen.
  - `MyFormatFloatShort(v)`: ≥ 10 Mio → `'%1.0n' Mio`, ≥ 1 Mio → `'%1.1n' Mio`, ≥ 100.000 → `v/1000` `'%1.0n' T`, sonst wie `MyFormatFloat`.
  - `MyFormatInt(i)`: `'%3.0n'` (mit Tausenderpunkten).
  - `MyFormatDate` → `'ddd dd.mm.yyyy'` (z.B. „Sa 12.09.2020“, Wochentagskürzel aus der Locale), `MyFormatDateShort` → `'dd.mm'`, `MyFormatTime` → `'hh:mm'`, `MyFormatDateTime` = Datum + ' ' + Zeit.
  - `MyFormatClickDiff(ms)`: „M Min S Sek“ (Minuten nur ab 60 s).
  - `DecToRom(n)`: Dezimalzahl → römische Zahl (für Mannschaftsnummern bzw. Anzeige).
- **Datum/Woche**:
  - `KalenderWoche(d)` = `WeekOf(d)` (ISO-8601-Woche).
  - `InternalWeekNumber(d) = (Trunc(d) − 2) div 7`: fortlaufende Wochennummer ab Montag 01.01.1900 (Delphi-TDateTime 2 = Montag). Performance-optimierter Ersatz für die KW, mit dem jahresübergreifend gerechnet werden kann.
  - `MondayBefore(d) = ((Trunc(d) − 2) div 7) * 7 + 2`: Montag der Woche (als TDateTime).
  - `TestDateRoutines()`: Selbsttest für 101 Tage ab 15.06.2015 (InternalWeekNumber gegen WeekOf, MondayBefore gegen EncodeDateWeek), wirft bei Abweichung eine Exception.
  - `FixDateTime(v)`: Decode und Encode, um Rundungsfehler bei TDateTime-Additionen zu beseitigen (Kommentar: 17:00 + 4:00 ≠ 21:00).
- **Parsing**: `MyStrToFloat(s)`: `StrToFloat` mit `DecimalSeparator='.'` (die restlichen FormatSettings-Felder sind **nicht initialisiert**, weil der lokale Record ohne `TFormatSettings.Create` angelegt wird).
  - Private Hilfen `DateFromString`/`TimeFromString` (`dd.mm.yyyy`, `hh:mm`), identisch mit denen in PlanDataObjects, hier aber nicht exportiert.
- **Logging**: `TraceString(s)` → `OutputDebugString` + `Log(s)`. `Log` hängt unter `LogCriticalSection` eine Zeile an `%TEMP%\Andi-Generator.log` an (Datei wird bei jedem Aufruf geöffnet und geschlossen). Beim ersten Aufruf wird „Programmstart: <Datum>“ geschrieben. Fehler werden verschluckt.
- **UI-Hilfen**:
  - `StartWaitCursor(cursor=crHourGlass): IInterface`: RAII. Setzt `Screen.Cursor` und stellt ihn beim Freigeben des Interfaces wieder her.
  - `FixControls(Win)`: rekursiv. Setzt die Formularschrift auf Arial, falls das Programm unter Wine läuft, und bestimmt die **TabOrder** geometrisch (oben vor unten, bei vertikaler Überlappung links vor rechts, `TabDavor`).
  - `FixFormSize(Win, W, H)`: DPI-Skalierung (`MulDiv(…, PixelsPerInch, 96)`), begrenzt auf 90 % der Bildschirmgröße.
  - `GetListComboValue`/`SetListComboValue`: ComboBox-Text ↔ Index.
- **Datei/System**:
  - `getAppLocalPath()`: `CSIDL_LOCAL_APPDATA` (z.B. `C:\Users\<u>\AppData\Local`).
  - `EncodeFileName(s)`: `#` und `< > " [ ] / \ ? * : |` → `#` + dreistelliger Dezimalcode (z.B. `/` → `#047`). `DecodeFileName` ist die Umkehrung (ungültige Sequenzen bleiben erhalten). `MakeValidFileName`: dieselben Zeichen → `-` (für Exportnamen).
  - `FilesFromDir(list, mask)`: `FindFirst/FindNext` mit `faNormal`, nur Dateinamen.
  - `GetFileVersionString(exe)`: `GetFileVersionInfo`/`VerQueryValue` → `a.b.c.d`.
- **initialization**: `DetermineUsedFont` (Wine-Erkennung über `ntdll.wine_get_version`), `LogCriticalSection` anlegen. **finalization**: freigeben.

## Fachliche Logik / Regeln
- Wochenlogik: Wochen beginnen am **Montag**. `InternalWeekNumber` und `MondayBefore` werden für Regeln wie „2 Spiele pro Woche“ (`mkt2SpieleProWoche`) bzw. wochenbasierte Kosten genutzt (Nutzung in PlanTypes).
- Dateinamen der gemerkten Pläne und des Plan-Verzeichnisses verwenden `EncodeFileName`. Das Format muss kompatibel bleiben, damit vorhandene Verzeichnisse gefunden werden.

## Daten & Persistenz
- Schreibt `%TEMP%\Andi-Generator.log` (nur, wenn `TraceString` aufgerufen wird, also im Wesentlichen mit `{$ifdef TRACE}`).
- Liest die Versionsressource der EXE.

## Threading / Performance
- `Log` ist über eine globale `TCriticalSection` threadsicher, blockiert aber alle Threads beim Datei-IO. Das ist relevant, wenn TRACE im Optimierer aktiv ist.
- `InternalWeekNumber`/`MondayBefore` sind reine Integer-Arithmetik (Hot-Path-tauglich). `KalenderWoche` (`WeekOf`) ist langsamer.
- Alle Formatierungsfunktionen verwenden die globalen FormatSettings (nicht threadsicher bei Änderung, werden aber nur gelesen).

## Plattformabhängigkeiten
- `OutputDebugString`, `GetTempPath`, `SHGetFolderLocation`/`SHGetPathFromIDListW`, `GetFileVersionInfo`/`VerQueryValue`, `GetModuleHandle`/`GetProcAddress` (Wine-Erkennung), `FindFirst` mit Windows-Maske.
- VCL: `Screen.Cursor`, `TWinControl`/`TabOrder`, `Screen.PixelsPerInch`, `TComboBox`.

## Migrationshinweise für C#
- Aufteilen:
  - `AndiGenerator.Core.Util.DateUtil` (Wochenlogik, `FixDateTime` entfällt bei `DateTime`-Ticks, Minuten-Rundung beibehalten)
  - `AndiGenerator.Core.Util.Format` (Anzeigeformate mit fester `CultureInfo("de-DE")`)
  - `AndiGenerator.Persistence.FileNameCodec` (Encode/Decode/MakeValid, **bitgenau kompatibel**)
  - `AndiGenerator.Infrastructure.Logging` (`ILogger`, z.B. Serilog-File-Sink in `Path.GetTempPath()`)
  - UI-Hilfen fallen in Avalonia weg: TabOrder über das Layout bzw. `TabIndex`, Wartecursor per `IsBusy`-Binding, DPI automatisch.
- `InternalWeekNumber` in C#: `(int)((date.Date - new DateTime(1900,1,1)).TotalDays) / 7`. Bei Verwendung von `DayNumber` (DateOnly) den Offset so wählen, dass Montag der Wochenbeginn ist, z.B. `(DateOnly.DayNumber) / 7` (DayNumber 0 = Mo 01.01.0001, daher Montag-basiert!). Integer-Division bei negativen Werten ist unkritisch.
- `KalenderWoche` → `ISOWeek.GetWeekOfYear`.
- `MyStrToFloat` → `double.Parse(s, CultureInfo.InvariantCulture)`.
- `MyFormatFloat` bzw. `%n` → `v.ToString("N2", de)` bzw. `"N0"`. Die Bedingung `(v < 10) && (v != 0)` exakt übernehmen.
- `ddd` in Delphi = kurzer Wochentagsname aus der Locale → .NET `"ddd"` mit de-DE ergibt „Sa“.
- `getAppLocalPath` → `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`.
- `GetFileVersionString` → `Assembly.GetEntryAssembly().GetName().Version`.
- Wine-Erkennung entfällt (native Linux-App).
- Aufwand: **S**. Einfache, gut isolierte Funktionen.

### Bugs/Auffälligkeiten
- Z. 85–88: `MyStrToFloat` verwendet einen nicht initialisierten lokalen `TFormatSettings`-Record (nur `DecimalSeparator` gesetzt, ThousandSeparator usw. zufällig). Funktioniert in der Praxis, ist aber undefiniert.
- Z. 108–117: `MyFormatClickDiff`: `Result` wird bei < 60 s nicht explizit initialisiert (Delphi-String-Result ist zwar in der Regel leer, bei Wiederverwendung der Result-Variable durch den Aufrufer aber nicht garantiert).
- Z. 122: `MyFormatFloat` formatiert negative Werte immer mit 2 Nachkommastellen (weil v < 10).
- Z. 419: `FindFirst(…, faNormal, …)` findet u.a. auch versteckte Dateien nicht. Unkritisch.

## Offene Fragen
- Muss die Log-Datei `%TEMP%\Andi-Generator.log` erhalten bleiben (Support-Fall) oder reicht ein moderner Logger?
- Soll die Anzeige-Locale fest Deutsch sein (die App ist fachlich deutsch) oder der System-Locale folgen?
