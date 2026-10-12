@echo off
rem Zeigt die Textzeilen eines PDF-Berichts, wie der Import sie liest (Fehlersuche). Ausgabe nach pdftext-log.txt.
chcp 65001 >nul
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "PDF=%~1"
if "%PDF%"=="" set "PDF=%~dp0Referenz\terminwuensche\Terminmeldung_Bezirksoberliga Rheinhessen Nord.pdf"
"%EXE%" pdftext "%PDF%" "%~dp0pdftext-log.txt"
pause
