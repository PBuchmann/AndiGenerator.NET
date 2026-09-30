# AndiGenerator.res

**Kategorie:** Projekt/Build (Ressourcen)

## Zweck / Inhalt
Kompilierte Windows-Ressourcendatei (15 KB): Hauptsymbol (MAINICON) und Versionsinfo (VERSIONINFO), wird per `{$R *.res}` in `AndiGenerator.dpr` eingebunden.

## Migrationshinweise für C#
- Icon → `<ApplicationIcon>` (Windows) bzw. Avalonia `WindowIcon`/App-Icons pro Plattform (.icns für macOS, PNG für Linux, Android/iOS-Asset-Sets).
- Versionsinfo → `<Version>`, `<Company>`, `<Product>` in der .csproj.

**Aufwand:** S.
