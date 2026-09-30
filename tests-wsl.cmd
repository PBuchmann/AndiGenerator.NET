@echo off
rem Baut die Solution und fuehrt alle Tests unter WSL (Linux) aus - dort greift Smart App Control nicht.
rem Protokoll: build-log.txt (wird von Claude gelesen). Einrichtung: Doku\WSL-TESTS.md
cd /d "%~dp0"
wsl.exe --cd "%~dp0." -e bash tools/tests-wsl.sh
echo.
echo Fertig. Das Protokoll steht in build-log.txt - Claude kann es jetzt lesen.
timeout /t 5 >nul
