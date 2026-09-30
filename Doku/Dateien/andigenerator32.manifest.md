# andigenerator32.manifest

**Kategorie:** Projekt/Build (Windows-Manifest)

## Zweck / Inhalt
Windows-Manifest für den Win32-Build (in .dproj für Win32-Konfigurationen referenziert). Enthält Common-Controls-v6-Abhängigkeit (Visual Styles), `requestedExecutionLevel asInvoker`, `dpiAware=true` (System-DPI, nicht Per-Monitor), supportedOS-IDs Vista…Windows 10, Copyright 2015 Andreas Hofmann.

## Migrationshinweise für C#
Siehe `andigenerator.manifest.md`: in .NET nur noch ein optionales `app.manifest` für den Windows-Build. 32-Bit-Build entfällt voraussichtlich (x64 + ARM64).

**Aufwand:** S.
