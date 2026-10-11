@echo off
rem Prueft einen Turnierplan (Jugend) gegen die Meldungen der Umfrage (andigen turnier pruefen).
rem   turnierpruefung.cmd "<click-TT-Datei.xml>" "<Meldungen.csv>"
rem Ohne Angaben: XML und CSV aus Referenz\jugend. Ausgabe zusaetzlich in turnier-log.txt (wird von Claude gelesen).
rem Vorher bauen.cmd ausfuehren.
chcp 65001 >nul
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
set "XML=%~1"
set "CSV=%~2"
if "%XML%"=="" for %%f in ("%~dp0Referenz\jugend\*.xml") do set "XML=%%f"
if "%CSV%"=="" for %%f in ("%~dp0Referenz\jugend\*.csv") do set "CSV=%%f"
if "%CSV%"=="" (
  echo Keine Meldungen gefunden - bitte die CSV der Umfrage nach Referenz\jugend legen.
  pause
  exit /b 1
)
"%EXE%" turnier pruefen "%XML%" "%CSV%" --ausgabe "%~dp0turnier-log.txt"
echo.
pause
