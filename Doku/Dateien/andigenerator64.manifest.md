# andigenerator64.manifest

**Kategorie:** Projekt/Build (Windows-Manifest)

## Zweck / Inhalt
Windows-Manifest für den Win64-Build (in .dproj referenziert). Wie `andigenerator32.manifest` (asInvoker, `dpiAware=true`, supportedOS Vista…10), **aber:** ohne Common-Controls-v6-Abhängigkeit (keine Visual Styles im 64-Bit-Build) und mit `processorArchitecture="ia64"` (Itanium) statt `amd64` – vermutlich ein Versehen.

## Migrationshinweise für C#
Siehe `andigenerator.manifest.md`. Ziel-Architekturen in .NET: win-x64, win-arm64, osx-arm64/x64, linux-x64/arm64.

**Aufwand:** S.
