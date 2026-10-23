@echo off
rem Zweite Validierung (nur die schweren Faelle): je Klasse 5 Verfahren a 5 Minuten (direkt, portfolio, teilneuplanung,
rem kombination, mehrfach), vorher bis zu 2 Minuten fuer das Ziel in A. Zusammen knapp 2 Stunden.
rem "schwer": Stufe B zusaetzlich mit 3-Tage-Abstand, 2 Spiele pro Woche und ungleich Heim/Auswaerts.
rem Ergebnisse: Ordner highsvalidierung2 (eine Datei je Klasse, wird von Claude gelesen).
rem Vorher bauen.cmd ausfuehren, die App schliessen und den Rechner in der Zeit moeglichst nicht belasten.
chcp 65001 >nul
cd /d "%~dp0"
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "ZIEL=%~dp0highsvalidierung2"
set "VERFAHREN=direkt,portfolio,teilneuplanung,kombination,mehrfach"
set "SCHWER=A:VereinsinterneSpieleAmAnfang,Hallenbelegung,ParalleleSpiele,Pflichtspieltage;B:Auswaertskoppel,Heimkoppel,Sperrtermine,Ausweichtermine,DreiTageAbstand,ZweiSpieleProWoche,UngleichHeimAuswaerts"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if not exist "%ZIEL%" mkdir "%ZIEL%"
call :lauf "verbandsoberliga" "Referenz\eingabe\Verbandsoberliga (2).xml" ""
call :lauf "verbandsoberliga-schwer" "Referenz\eingabe\Verbandsoberliga (2).xml" "%SCHWER%"
call :lauf "r21-verbandsoberliga-schwer" "testdaten\referenz\eingabe\R21_Verbandsoberliga (1).xml" "%SCHWER%"
call :lauf "r16-bezirksoberliga-nord-schwer" "testdaten\referenz\eingabe\R16_Bezirksoberliga_Rheinhessen_Nord (1).xml" "%SCHWER%"
echo.
echo ================================ Alle Klassen ================================
for %%f in ("%ZIEL%\*.txt") do (
  echo.
  echo %%~nf
  findstr /B /C:"Verfahren " /C:"direkt " /C:"portfolio " /C:"teilneuplanung " /C:"kombination " /C:"mehrfach " "%%f"
)
echo.
echo Fertig. Ergebnisse im Ordner highsvalidierung2 - Claude kann sie jetzt lesen.
pause
goto :eof

:lauf
echo.
echo ################################ %~1 ################################
if "%~3"=="" (
  "%EXE%" highsvergleich "%~dp0%~2" --sekunden 300 --ziel-a auto --intervall 5 --optionen auto --verfahren %VERFAHREN% --ausgabe "%ZIEL%\%~1.txt"
) else (
  "%EXE%" highsvergleich "%~dp0%~2" --sekunden 300 --ziel-a auto --intervall 5 --optionen auto --verfahren %VERFAHREN% --stufen "%~3" --ausgabe "%ZIEL%\%~1.txt"
)
goto :eof
