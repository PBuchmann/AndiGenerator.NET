@echo off
rem Vergleich der Verfahren mit HiGHS an der Verbandsoberliga (Referenz\eingabe\Verbandsoberliga (2).xml): Einteilung wie
rem gespeichert, die ganze Runde, Ziel A hoechstens 10 und moeglichst wenige B-Verstoesse. Nur HiGHS, ohne Inseln.
rem Je Verfahren 5 Minuten (7 Verfahren, zusammen gut 35 Minuten); alle 5 Sekunden ein Zwischenstand im Fenster.
rem Ergebnis: highsvergleich.txt (wird von Claude gelesen), beste Plaene je Verfahren im Ordner highsvergleich.
rem Vorher bauen.cmd ausfuehren, die App schliessen und den Rechner in der Zeit moeglichst nicht belasten.
chcp 65001 >nul
cd /d "%~dp0"
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "STAFFEL=%~dp0Referenz\eingabe\Verbandsoberliga (2).xml"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if not exist "%STAFFEL%" (
  echo "%STAFFEL%" nicht gefunden.
  pause
  exit /b 1
)
"%EXE%" highsvergleich "%STAFFEL%" --sekunden 300 --ziel-a 10 --intervall 5 --optionen auto --ausgabe "%~dp0highsvergleich.txt" --plaene "%~dp0highsvergleich"
echo.
echo Fertig. Ergebnis in highsvergleich.txt - Claude kann es jetzt lesen.
pause
