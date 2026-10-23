@echo off
rem Inseln bleiben verschieden: Automodus wie in der Oberflaeche (Inseln, Untergrenze und Teilneuplanung nebenher).
rem 1) Verbandsoberliga ohne und mit zwei HiGHS-Linien, Startwert 1 und 2, je 10 Minuten (wie linienvergleich.cmd).
rem 2) Kleinere Aufgaben auf wenigen Kernen: R16 und R21 mit 4 Rechen-Threads ohne Linien, Startwert 1 und 2, je 5 Minuten.
rem Zusammen etwa eine Stunde. Ergebnisse: Ordner vielfaltvergleich (wird von Claude gelesen).
rem Vorher bauen.cmd ausfuehren, die App schliessen und den Rechner in der Zeit moeglichst nicht belasten.
chcp 65001 >nul
cd /d "%~dp0"
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "VOL=%~dp0Referenz\eingabe\Verbandsoberliga (2).xml"
set "R16=%~dp0testdaten\referenz\eingabe\R16_Bezirksoberliga_Rheinhessen_Nord (1).xml"
set "R21=%~dp0testdaten\referenz\eingabe\R21_Verbandsoberliga (1).xml"
set "ZIEL=%~dp0vielfaltvergleich"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if not exist "%ZIEL%" mkdir "%ZIEL%"
for %%s in (1 2) do (
  for %%l in (0 2) do (
    echo.
    echo ################################ Verbandsoberliga, Linien %%l, Startwert %%s ################################
    "%EXE%" optimieren "%VOL%" --leer --nebenher --neuplanen --linien %%l --sekunden 600 --intervall 30 --startwert %%s --optionen auto --ausgabe "%ZIEL%\vol-linien%%l-start%%s.txt"
  )
)
for %%s in (1 2) do (
  echo.
  echo ################################ R16, 4 Threads, Startwert %%s ################################
  "%EXE%" optimieren "%R16%" --leer --nebenher --neuplanen --linien 0 --kerne 4 --sekunden 300 --intervall 30 --startwert %%s --optionen auto --ausgabe "%ZIEL%\r16-kerne4-start%%s.txt"
  echo.
  echo ################################ R21, 4 Threads, Startwert %%s ################################
  "%EXE%" optimieren "%R21%" --leer --nebenher --neuplanen --linien 0 --kerne 4 --sekunden 300 --intervall 30 --startwert %%s --optionen auto --ausgabe "%ZIEL%\r21-kerne4-start%%s.txt"
)
echo.
echo ================================ Ergebnis ================================
for %%f in ("%ZIEL%\*.txt") do (
  echo %%~nf
  findstr /B /C:"Automodus: bester" "%%f"
)
echo.
echo Fertig. Ergebnisse im Ordner vielfaltvergleich - Claude kann sie jetzt lesen.
pause
