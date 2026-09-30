@echo off
rem Optimiert eine Staffel mit der neuen Engine (andigen optimieren). XML-Datei auf dieses Skript ziehen oder:
rem   optimieren.cmd "<click-TT-Datei.xml>" [--sekunden 60] [--leer] [--optionen auto] [--plan <datei.xml>] ...
rem Ausgabe zusaetzlich in optimierung-log.txt (wird von Claude gelesen). Vorher bauen.cmd ausfuehren.
chcp 65001 >nul
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if "%~1"=="" (
  "%EXE%" optimieren --hilfe
  pause
  exit /b 1
)
"%EXE%" optimieren %* --ausgabe "%~dp0optimierung-log.txt"
echo.
pause
