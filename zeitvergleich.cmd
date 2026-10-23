@echo off
rem Wie frueh ein guter Plan da ist: Automodus wie in der Oberflaeche (Inseln, Untergrenze und Teilneuplanung nebenher).
rem 1) Verbandsoberliga ohne und mit zwei HiGHS-Linien, Startwert 1 und 2, je 10 Minuten (wie linienvergleich.cmd).
rem 2) Kleinere Aufgaben auf wenigen Kernen: R16 und R21 mit 4 Rechen-Threads ohne Linien, Startwert 1 und 2, je 5 Minuten.
rem Zusammen etwa eine Stunde. Ergebnisse: Ordner zeitvergleich (wird von Claude gelesen).
rem Vorher bauen.cmd ausfuehren, die App schliessen und den Rechner in der Zeit moeglichst nicht belasten.
rem Der Rechner darf in der Zeit nicht in den Energiesparmodus gehen (Netzteil an, Deckel offen). Abgebrochen oder
rem unterbrochen: einfach neu starten - fertige Laeufe werden uebersprungen.
chcp 65001 >nul
cd /d "%~dp0"
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "VOL=%~dp0Referenz\eingabe\Verbandsoberliga (2).xml"
set "R16=%~dp0testdaten\referenz\eingabe\R16_Bezirksoberliga_Rheinhessen_Nord (1).xml"
set "R21=%~dp0testdaten\referenz\eingabe\R21_Verbandsoberliga (1).xml"
set "ZIEL=%~dp0zeitvergleich"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if not exist "%ZIEL%" mkdir "%ZIEL%"
for %%s in (1 2) do (
  for %%l in (0 2) do call :lauf "Verbandsoberliga, Linien %%l, Startwert %%s" "%VOL%" "vol-linien%%l-start%%s" "--linien %%l --sekunden 600 --startwert %%s" "10:00"
)
for %%s in (1 2) do (
  call :lauf "R16, 4 Threads, Startwert %%s" "%R16%" "r16-kerne4-start%%s" "--linien 0 --kerne 4 --sekunden 300 --startwert %%s" "5:00"
  call :lauf "R21, 4 Threads, Startwert %%s" "%R21%" "r21-kerne4-start%%s" "--linien 0 --kerne 4 --sekunden 300 --startwert %%s" "5:00"
)
echo.
echo ================================ Ergebnis ================================
for %%f in ("%ZIEL%\*.txt") do (
  echo %%~nf
  findstr /B /C:"Automodus: bester" /C:"Verlauf:" "%%f"
)
echo.
echo Fertig. Ergebnisse im Ordner zeitvergleich - Claude kann sie jetzt lesen.
pause
goto :eof

rem Ein Lauf; uebersprungen, wenn sein Ergebnis schon vollstaendig ist (Verlauf bis zum Ende) - so laesst sich ein
rem abgebrochener Vergleich einfach neu starten.
:lauf
findstr /B /C:"Verlauf:" "%ZIEL%\%~3.txt" 2>nul | findstr /C:" %~5 " >nul && (
  echo %~1: schon fertig, uebersprungen
  goto :eof
)
echo.
echo ################################ %~1 ################################
"%EXE%" optimieren "%~2" --leer --nebenher --neuplanen %~4 --intervall 30 --optionen auto --ausgabe "%ZIEL%\%~3.txt"
goto :eof
