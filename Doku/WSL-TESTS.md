# Tests unter WSL (Linux)

> Stand 27.09.2026: Auf Peters Rechner ist Smart App Control abgeschaltet, die Tests laufen wieder direkt mit `bauen.cmd`.
> Dieser Weg bleibt als Ausweichmöglichkeit für Rechner mit aktivem Smart App Control.

Smart App Control blockiert unter Windows jede neu gebaute, unsignierte DLL – unabhängig vom Ablageort
(Policy-ID `{0283ac0f-fff1-49ae-ada1-8a933130cad6}`). Deshalb laufen die Tests unter WSL. Dort gibt es kein Smart App Control.
Unter Windows wird weiterhin mit `bauen.cmd` gebaut (Build und Analyzer). Die Tests starten dort aber nicht immer.

## Einmalige Einrichtung

1. **WSL mit Ubuntu installieren:** PowerShell *als Administrator* öffnen und diesen Befehl ausführen. Danach Windows neu starten.

   ```
   wsl --install -d Ubuntu-24.04
   ```

   Beim ersten Start von „Ubuntu“ (Startmenü) legst du einen Linux-Benutzernamen und ein Passwort fest.

2. **.NET-10-SDK in Ubuntu installieren:** Im Ubuntu-Fenster diese Befehle ausführen:

   ```
   sudo apt update && sudo apt install -y libicu-dev wget
   wget https://dot.net/v1/dotnet-install.sh -O ~/dotnet-install.sh
   bash ~/dotnet-install.sh --channel 10.0
   ~/.dotnet/dotnet --list-sdks
   ```

   Die letzte Zeile muss eine Version `10.0.x` anzeigen.

## Benutzung

Doppelklick auf **`tests-wsl.cmd`** im Projektordner. Das Skript baut und testet unter Linux. Das Protokoll steht wie
bei `bauen.cmd` in `build-log.txt`. Der erste Lauf dauert länger, weil die NuGet-Pakete geladen werden.

- Die Build-Ausgaben liegen im Linux-Dateisystem unter `~/.cache/andigenerator.net/artifacts`, nicht in OneDrive
  (`Directory.Build.props`).
- Die Quellen werden direkt aus dem Windows-Ordner gelesen (`/mnt/c/...`). Es gibt also keine zweite Kopie.
- Die Testdaten liegen anonymisiert im Repository (`testdaten/`, siehe TESTDATEN.md) und werden wie unter Windows gefunden.
