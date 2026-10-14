@echo off
rem Erzeugt Doku\Anleitung.pdf aus Doku\ANLEITUNG.md neu (pandoc und Edge, ohne Installation; siehe tools\anleitung\README.md).
rem Dauert beim ersten Mal laenger (pandoc und NuGet-Pakete werden geladen). Protokoll: anleitung-log.txt.
chcp 65001 >nul
cd /d "%~dp0"
set "LOG=%~dp0anleitung-log.txt"
echo Erzeuge die Anleitung als PDF ...
powershell -NoProfile -ExecutionPolicy Bypass -File tools\anleitung\anleitung.ps1 > "%LOG%" 2>&1
set RC=%ERRORLEVEL%
type "%LOG%"
echo.
if not "%RC%"=="0" (
  echo ERGEBNIS: FEHLGESCHLAGEN - das Protokoll steht in anleitung-log.txt.
  pause
  goto :eof
)
echo ERGEBNIS: Doku\Anleitung.pdf ist neu erzeugt.
timeout /t 10 >nul
