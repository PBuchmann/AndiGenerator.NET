@echo off
rem Validierung der Verfahren mit HiGHS an mehreren Klassen (nur HiGHS, ohne Inseln), je Klasse 5 Verfahren a 3 Minuten,
rem vorher bis zu 2 Minuten fuer das Ziel in A (was HiGHS in Stufe A findet). Zusammen gut 2 1/2 Stunden.
rem "schwer": Stufe B zusaetzlich mit 3-Tage-Abstand, 2 Spiele pro Woche und ungleich Heim/Auswaerts.
rem Ergebnisse: Ordner highsvalidierung (eine Datei je Klasse, wird von Claude gelesen).
rem Vorher bauen.cmd ausfuehren, die App schliessen und den Rechner in der Zeit moeglichst nicht belasten.
chcp 65001 >nul
cd /d "%~dp0"
set "EXE=%LOCALAPPDATA%\AndiGenerator.NET\artifacts\bin\AndiGenerator.Cli\release\andigen.exe"
set "ZIEL=%~dp0highsvalidierung"
set "SCHWER=A:VereinsinterneSpieleAmAnfang,Hallenbelegung,ParalleleSpiele,Pflichtspieltage;B:Auswaertskoppel,Heimkoppel,Sperrtermine,Ausweichtermine,DreiTageAbstand,ZweiSpieleProWoche,UngleichHeimAuswaerts"
if not exist "%EXE%" (
  echo andigen.exe nicht gefunden - bitte zuerst bauen.cmd ausfuehren.
  pause
  exit /b 1
)
if not exist "%ZIEL%" mkdir "%ZIEL%"
call :lauf "verbandsoberliga" "Referenz\eingabe\Verbandsoberliga (2).xml" ""
call :lauf "verbandsoberliga-schwer" "Referenz\eingabe\Verbandsoberliga (2).xml" "%SCHWER%"
rem Dateinamen mit Umlaut ueber Platzhalter (S*d), damit die Codepage keine Rolle spielt.
for %%f in ("Referenz\eingabe\Bezirksliga_Rheinhessen_S*d (4).xml") do (
  call :lauf "bezirksliga-sued" "%%~f" ""
  call :lauf "bezirksliga-sued-schwer" "%%~f" "%SCHWER%"
)
for %%f in ("testdaten\referenz\eingabe\R22_Verbandsliga_Rheinland_S*d-West.xml") do call :lauf "r22-verbandsliga-rsw" "%%~f" ""
call :lauf "r21-verbandsoberliga" "testdaten\referenz\eingabe\R21_Verbandsoberliga (1).xml" ""
call :lauf "r16-bezirksoberliga-nord" "testdaten\referenz\eingabe\R16_Bezirksoberliga_Rheinhessen_Nord (1).xml" ""
call :lauf "r26-koppeltermine" "testdaten\referenz\eingabe\R26_Liga mit Koppelterminen.xml" ""
echo.
echo ================================ Alle Klassen ================================
for %%f in ("%ZIEL%\*.txt") do (
  echo.
  echo %%~nf
  findstr /B /C:"Verfahren " /C:"kette " /C:"direkt " /C:"portfolio " /C:"teilneuplanung " /C:"kombination " "%%f"
)
echo.
echo Fertig. Ergebnisse im Ordner highsvalidierung - Claude kann sie jetzt lesen.
pause
goto :eof

:lauf
echo.
echo ################################ %~1 ################################
if "%~3"=="" (
  "%EXE%" highsvergleich "%~dp0%~2" --sekunden 180 --ziel-a auto --intervall 5 --optionen auto --ausgabe "%ZIEL%\%~1.txt"
) else (
  "%EXE%" highsvergleich "%~dp0%~2" --sekunden 180 --ziel-a auto --intervall 5 --optionen auto --stufen "%~3" --ausgabe "%ZIEL%\%~1.txt"
)
goto :eof
