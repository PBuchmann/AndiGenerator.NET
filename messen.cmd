@echo off
rem Geschwindigkeitsmessungen der Engine (Tests der Kategorie "Messung"). Ergebnisse werden an build-messung.txt
rem angehaengt, das Protokoll steht in messen-log.txt. Vorher bauen.cmd ausfuehren.
chcp 65001 >nul
cd /d "%~dp0"
set "LOG=%~dp0messen-log.txt"
echo ===== START %date% %time% > "%LOG%"
echo Messe ...
dotnet test tests\AndiGenerator.Persistence.Tests\AndiGenerator.Persistence.Tests.csproj -c Release --no-build -nologo --filter "Kategorie=Messung" --logger "console;verbosity=normal" >> "%LOG%" 2>&1
echo ----- TEST Exitcode %ERRORLEVEL% >> "%LOG%"
echo ===== ENDE %date% %time% >> "%LOG%"
echo.
echo Fertig. Ergebnisse in build-messung.txt - Claude kann sie jetzt lesen.
timeout /t 5 >nul
