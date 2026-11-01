@echo off
rem Einfacher Modus vollautomatisch ueber mehrere Klassen (andigen einfachreihe), wie ein Klassenleiter ihn nutzt:
rem je Klasse 2 Minuten mit allen Kernen, Zwischenstand nach 1 Minute, dazu der Automodus mit HiGHS zum Vergleich.
rem Ordner (oder einzelne XML-Dateien) auf dieses Skript ziehen; ohne Angabe: Referenz\eingabe.
rem Ergebnis: Unterordner "einfachreihe" neben den Eingaben (Plaene, Protokolle, einfachreihe.csv),
rem Bildschirmausgabe zusaetzlich in einfachreihe-log.txt (wird von Claude gelesen). Vorher bauen.cmd ausfuehren.
rem Weitere Optionen anhaengen, z. B.:  einfachreihe.cmd "Referenz\eingabe" --sekunden 300 --kerne 4
chcp 65001 >nul
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if "%~1"=="" (
  "%EXE%" einfachreihe "%~dp0Referenz\eingabe" --vergleich --ausgabe "%~dp0einfachreihe-log.txt"
) else (
  "%EXE%" einfachreihe %* --vergleich --ausgabe "%~dp0einfachreihe-log.txt"
)
echo.
pause
