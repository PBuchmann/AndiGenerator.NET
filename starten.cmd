@echo off
rem Startet die neue Oberflaeche (vorher bauen.cmd ausfuehren).
chcp 65001 >nul
cd /d "%~dp0"
rem Strg+Umschalt+F12 speichert das aktive Fenster als PNG (Bilder fuer die Anleitung).
set "ANDIGEN_BILDSCHIRMFOTOS=%~dp0Bildschirmfotos"
dotnet run --project src\AndiGenerator.Desktop -c Release --no-build
if errorlevel 1 pause
