@echo off
rem Automodus wie in der Oberflaeche (Inseln, Untergrenze und Teilneuplanung nebenher) an der Verbandsoberliga, ohne und mit
rem zwei HiGHS-Linien, je mit Startwert 1 und 2: 4 Laeufe a 10 Minuten Rechenzeit der Inseln, zusammen etwa 45 Minuten.
rem Ergebnisse: Ordner linienvergleich (wird von Claude gelesen).
rem Vorher bauen.cmd ausfuehren, die App schliessen und den Rechner in der Zeit moeglichst nicht belasten.
chcp 65001 >nul
cd /d "%~dp0"
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "STAFFEL=%~dp0Referenz\eingabe\Verbandsoberliga (2).xml"
set "ZIEL=%~dp0linienvergleich"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if not exist "%ZIEL%" mkdir "%ZIEL%"
for %%s in (1 2) do (
  for %%l in (0 2) do (
    echo.
    echo ################################ Linien %%l, Startwert %%s ################################
    "%EXE%" optimieren "%STAFFEL%" --leer --nebenher --neuplanen --linien %%l --sekunden 600 --intervall 30 --startwert %%s --optionen auto --ausgabe "%ZIEL%\linien%%l-start%%s.txt"
  )
)
echo.
echo ================================ Ergebnis ================================
for %%f in ("%ZIEL%\*.txt") do (
  echo %%~nf
  findstr /B /C:"Automodus: bester" "%%f"
)
echo.
echo Fertig. Ergebnisse im Ordner linienvergleich - Claude kann sie jetzt lesen.
pause
