# andigenerator.manifest

**Kategorie:** Projekt/Build (Windows-Manifest)

## Zweck / Inhalt
Windows-Anwendungsmanifest. Inhalt (supportedOS Vista…10, `dpiAware=true`) ist **vollständig auskommentiert** – effektiv ein leeres Manifest. Wird in der .dproj nicht referenziert (dort `andigenerator32/64.manifest` bzw. `$(BDS)\bin\default_app.manifest`).

## Plattformabhängigkeiten
Nur Windows.

## Migrationshinweise für C#
- .NET erzeugt ein Standardmanifest; ggf. `app.manifest` für Windows mit `PerMonitorV2`-DPI-Awareness, `supportedOS` Win10/11 und `asInvoker`.
- Avalonia/MAUI regeln DPI selbst; Manifest nur für den Windows-Build nötig.

**Aufwand:** S.
