@echo off
rem Erzeugt die Bildschirmfotos der Anleitung neu (Doku\Anleitung\Bilder) - ohne Bildschirm, mit anonymisierten Testdaten.
rem Laeuft etwa eine Minute (Kostenoptimierung und Automodus rechnen kurz). Protokoll: bildschirmfotos-log.txt.
chcp 65001 >nul
cd /d "%~dp0"
set "LOG=%~dp0bildschirmfotos-log.txt"
set "ANDIGEN_BILDER=%~dp0Doku\Anleitung\Bilder"
echo Erzeuge Bildschirmfotos in %ANDIGEN_BILDER% ...
dotnet test tests\AndiGenerator.UI.Tests -c Release -nologo --filter "FullyQualifiedName~Bildschirmfotos" --logger "console;verbosity=normal" > "%LOG%" 2>&1
set RC=%ERRORLEVEL%
echo.
findstr /C:": error " /C:"[FAIL]" "%LOG%"
if not "%RC%"=="0" (
  echo ERGEBNIS: FEHLGESCHLAGEN - das Protokoll steht in bildschirmfotos-log.txt.
  pause
  goto :eof
)
echo ERGEBNIS: Bilder erzeugt. Mit git diff bzw. im Explorer pruefen und danach die PDF neu erzeugen.
timeout /t 10 >nul
