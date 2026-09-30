@echo off
rem Baut die Solution und fuehrt alle Tests aus. Protokoll: build-log.txt (wird von Claude gelesen).
rem Am Ende steht eine Zusammenfassung im Fenster: Fehler, Warnungen und fehlgeschlagene Tests.
chcp 65001 >nul
cd /d "%~dp0"
set "LOG=%~dp0build-log.txt"
tasklist /FI "IMAGENAME eq AndiGenerator.NET.exe" 2>nul | find /I "AndiGenerator.NET.exe" >nul
if not errorlevel 1 (
  echo AndiGenerator.NET.exe laeuft noch - bitte zuerst schliessen, sonst kann sie nicht neu gebaut werden.
  pause
)
set TESTRC=-
echo ===== START %date% %time% > "%LOG%"
echo ----- dotnet --list-sdks >> "%LOG%"
dotnet --list-sdks >> "%LOG%" 2>&1
echo Baue ...
echo ----- BUILD >> "%LOG%"
dotnet build AndiGenerator.slnx -c Release -nologo -v:minimal >> "%LOG%" 2>&1
set BUILDRC=%ERRORLEVEL%
echo ----- BUILD Exitcode %BUILDRC% >> "%LOG%"
if not "%BUILDRC%"=="0" goto ende
echo Teste ...
echo ----- TEST >> "%LOG%"
dotnet test AndiGenerator.slnx -c Release --no-build -nologo --logger "console;verbosity=normal" >> "%LOG%" 2>&1
set TESTRC=%ERRORLEVEL%
echo ----- TEST Exitcode %TESTRC% >> "%LOG%"
:ende
echo ===== ENDE %date% %time% >> "%LOG%"
echo.
echo ================================ ZUSAMMENFASSUNG ================================
findstr /C:": error " "%LOG%" >nul && (
  echo FEHLER beim Bauen:
  findstr /C:": error " "%LOG%" | sort /unique
  echo.
)
findstr /C:": warning " "%LOG%" >nul && (
  echo WARNUNGEN:
  findstr /C:": warning " "%LOG%" | sort /unique
  echo.
)
findstr /C:"[FAIL]" "%LOG%" >nul && (
  echo FEHLGESCHLAGENE TESTS:
  findstr /C:"[FAIL]" "%LOG%"
  echo.
)
findstr /C:"Gesamtzahl Tests" /C:"     Bestanden:" /C:"     Nicht bestanden:" "%LOG%"
echo.
if not "%BUILDRC%"=="0" (
  echo ERGEBNIS: BAUEN FEHLGESCHLAGEN - Tests wurden nicht ausgefuehrt.
  goto warten
)
if not "%TESTRC%"=="0" (
  echo ERGEBNIS: TESTS FEHLGESCHLAGEN.
  goto warten
)
findstr /C:": warning " "%LOG%" >nul && (
  echo ERGEBNIS: Gebaut, alle Tests gruen - aber mit Warnungen.
  goto warten
)
echo ERGEBNIS: ALLES OK - gebaut ohne Warnungen, alle Tests gruen.
echo Das Protokoll steht in build-log.txt - Claude kann es jetzt lesen.
timeout /t 10 >nul
goto :eof
:warten
echo Das Protokoll steht in build-log.txt - Claude kann es jetzt lesen.
pause
