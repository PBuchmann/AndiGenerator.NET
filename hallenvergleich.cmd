@echo off
rem Vergleich der Korrektur "Hallen nach Namen" mit dem Original-Verhalten (alle anderen Korrekturen bleiben an):
rem Verbandsoberliga, je Variante drei reproduzierbare Laeufe (1 Kern, feste Startwerte, 2 Mio. Durchlaeufe) aus
rem leerem Plan (zusammen etwa 6-10 Minuten). Danach werden alle Plaene in beiden Kostenmodellen bewertet.
rem Ergebnisse im Ordner hallenvergleich (wird von Claude gelesen). Vorher bauen.cmd ausfuehren.
chcp 65001 >nul
cd /d "%~dp0"
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "ZIEL=%~dp0hallenvergleich"
set "XML=%~dp0Referenz\eingabe\R21_Verbandsoberliga (1).xml"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if not exist "%ZIEL%" mkdir "%ZIEL%"
for %%s in (1 2 3) do (
  echo.
  echo ======== Hallen nach Namen, Startwert %%s ========
  "%EXE%" optimieren "%XML%" --leer --kerne 1 --startwert %%s --durchlaeufe 2000000 --intervall 30 --plan "%ZIEL%\namen-%%s.xml" --ausgabe "%ZIEL%\namen-%%s.txt"
  echo.
  echo ======== Hallen wie im Original, Startwert %%s ========
  "%EXE%" optimieren "%XML%" --leer --kerne 1 --startwert %%s --durchlaeufe 2000000 --intervall 30 --hallen-wie-original --plan "%ZIEL%\original-%%s.xml" --ausgabe "%ZIEL%\original-%%s.txt"
)
"%EXE%" bewerten "%XML%" "%ZIEL%\namen-1.xml" "%ZIEL%\namen-2.xml" "%ZIEL%\namen-3.xml" "%ZIEL%\original-1.xml" "%ZIEL%\original-2.xml" "%ZIEL%\original-3.xml" --details --ausgabe "%ZIEL%\bewertung-namen.txt"
"%EXE%" bewerten "%XML%" "%ZIEL%\namen-1.xml" "%ZIEL%\namen-2.xml" "%ZIEL%\namen-3.xml" "%ZIEL%\original-1.xml" "%ZIEL%\original-2.xml" "%ZIEL%\original-3.xml" --hallen-wie-original --details --ausgabe "%ZIEL%\bewertung-original.txt"
echo.
echo Fertig. Ergebnisse im Ordner hallenvergleich - Claude kann sie jetzt lesen.
echo ===== ENDE %DATE% %TIME% > "%ZIEL%\fertig.txt"
pause
