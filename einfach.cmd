@echo off
rem Einfacher Modus zum Ausprobieren (andigen einfach --mitbestimmen). XML-Datei auf dieses Skript ziehen oder:
rem   einfach.cmd "<click-TT-Datei.xml>" [--optionen auto] [--sekunden 600] ...
rem Im Fenster: Enter zeigt Beweisstand und Vorschlaege, "2" waehlt Vorschlag 2, "2a" dazu Hebel a, "x" beendet.
rem Ausgabe zusaetzlich in einfach-log.txt (wird von Claude gelesen), bester Plan als "<Datei> einfach.xml" daneben.
rem Vorher bauen.cmd ausfuehren.
chcp 65001 >nul
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if "%~1"=="" (
  "%EXE%" einfach --hilfe
  pause
  exit /b 1
)
"%EXE%" einfach %* --mitbestimmen --plan "%~dpn1 einfach.xml" --ausgabe "%~dp0einfach-log.txt"
echo.
pause
