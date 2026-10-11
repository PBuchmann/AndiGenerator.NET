@echo off
rem Vergleich der Korrekturen (Klasse-3-Befunde, Regel fuer den Pflichtspieltag) mit dem Original-Verhalten:
rem drei Staffeln mit ungerader Mannschaftszahl, je Variante zwei Laeufe zu 120 s Kostenoptimierung aus leerem Plan
rem (zusammen gut 25 Minuten). Danach werden alle Plaene in beiden Kostenmodellen bewertet. Ergebnisse im Ordner
rem korrekturvergleich (wird von Claude gelesen). Vorher bauen.cmd ausfuehren und den Rechner moeglichst nicht belasten.
chcp 65001 >nul
cd /d "%~dp0"
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "ZIEL=%~dp0korrekturvergleich"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if not exist "%ZIEL%" mkdir "%ZIEL%"
call :staffel R3 "%~dp0testdaten\referenz\eingabe\R3_Damen_Bezirksoberliga_Rheinhessen (1).xml"
call :staffel R19 "%~dp0testdaten\referenz\eingabe\R19_1__Kreisklasse (7).xml"
call :staffel KK3 "%~dp0testdaten\referenz\perf\3__Kreisklasse (8).xml"
echo.
echo Fertig. Ergebnisse im Ordner korrekturvergleich - Claude kann sie jetzt lesen.
echo ===== ENDE %DATE% %TIME% > "%ZIEL%\fertig.txt"
pause
goto :eof

:staffel
for %%l in (1 2) do (
  echo.
  echo ======== %1 neu, Lauf %%l ========
  "%EXE%" optimieren %2 --leer --sekunden 120 --intervall 30 --plan "%ZIEL%\%1-neu-%%l.xml" --ausgabe "%ZIEL%\%1-neu-%%l.txt"
  echo.
  echo ======== %1 original, Lauf %%l ========
  "%EXE%" optimieren %2 --leer --sekunden 120 --intervall 30 --original --plan "%ZIEL%\%1-original-%%l.xml" --ausgabe "%ZIEL%\%1-original-%%l.txt"
)
"%EXE%" bewerten %2 "%ZIEL%\%1-neu-1.xml" "%ZIEL%\%1-neu-2.xml" "%ZIEL%\%1-original-1.xml" "%ZIEL%\%1-original-2.xml" --ausgabe "%ZIEL%\%1-bewertung-neu.txt"
"%EXE%" bewerten %2 "%ZIEL%\%1-neu-1.xml" "%ZIEL%\%1-neu-2.xml" "%ZIEL%\%1-original-1.xml" "%ZIEL%\%1-original-2.xml" --original --ausgabe "%ZIEL%\%1-bewertung-original.txt"
goto :eof
