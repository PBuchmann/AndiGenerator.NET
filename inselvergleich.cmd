@echo off
rem Vergleich der Zahl der Inseln (2, 4, 8, 16): je 5 Minuten Kostenoptimierung der Verbandsoberliga aus einem leeren
rem Plan, zusammen gut 20 Minuten. Der Verlauf steht im Fenster; je Variante zusaetzlich in inselvergleich-N.txt
rem (wird von Claude gelesen). Vorher bauen.cmd ausfuehren und den Rechner in der Zeit moeglichst nicht belasten.
chcp 65001 >nul
cd /d "%~dp0"
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "STAFFEL=%~dp0testdaten\referenz\perf\Verbandsoberliga (1).xml"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
for %%n in (2 4 8 16) do (
  echo.
  echo ================================ %%n Inseln ================================
  "%EXE%" optimieren "%STAFFEL%" --leer --sekunden 300 --intervall 15 --inseln %%n --ausgabe "%~dp0inselvergleich-%%n.txt"
)
echo.
echo Fertig. Ergebnisse in inselvergleich-2.txt ... inselvergleich-16.txt - Claude kann sie jetzt lesen.
pause
