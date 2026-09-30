@echo off
rem Misst die Testabdeckung (Cobertura-XML unter TestResults\Abdeckung, wird von Claude gelesen). Vorher bauen.cmd ausfuehren.
chcp 65001 >nul
cd /d "%~dp0"
set "LOG=%~dp0abdeckung-log.txt"
echo ===== START %date% %time% > "%LOG%"
dotnet test AndiGenerator.slnx -c Release --no-build -nologo --collect "Code Coverage;Format=cobertura" --results-directory TestResults\Abdeckung >> "%LOG%" 2>&1
echo ----- TEST Exitcode %ERRORLEVEL% >> "%LOG%"
echo ===== ENDE %date% %time% >> "%LOG%"
echo.
echo Fertig. Ergebnis unter TestResults\Abdeckung - Claude kann es jetzt lesen.
timeout /t 5 >nul
