# Drittbibliotheken

Für alles, was mit dem Programm ausgeliefert oder im Quellcode als Bibliothek verwendet wird, sind nur MIT-, Apache-2.0-, BSD- oder LGPL-lizenzierte Pakete zulässig (MIGRATIONSPLAN E11). Reine Entwicklungswerkzeuge sind davon ausgenommen (siehe unten). Jede neue Abhängigkeit wird hier eingetragen. Die gültigen Versionen der NuGet-Pakete stehen in [`Directory.Packages.props`](Directory.Packages.props) (Dependabot hält sie aktuell), die der GitHub Actions in den Workflows unter `.github/workflows`.

Ausnahme Schriften: Die SIL Open Font License (OFL 1.1) erlaubt das Einbetten und Weitergeben mit GPL-Software; der Lizenztext liegt neben den Schriftdateien, der reservierte Name „Plex“ wird nicht für veränderte Schriften verwendet.

| Paket | Lizenz | Verwendung |
|---|---|---|
| .NET 10 Runtime/BCL | MIT | Laufzeit |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent | MIT | Oberfläche (E2) |
| Avalonia.Skia (mit SkiaSharp, HarfBuzzSharp) | MIT | PDF-Ausdruck (`SKDocument`, Phase 7) |
| Dock.Avalonia, Dock.Avalonia.Themes.Fluent, Dock.Model.Mvvm | MIT | Andock-Layout der Ansichten (E15) |
| CommunityToolkit.Mvvm | MIT | ViewModels (E3) |
| Velopack | MIT | Installation und Updates über GitHub Releases (E12); Version gemeinsam mit `VPK_VERSION` in `release.yml` |
| Schriften IBM Plex Sans und IBM Plex Mono (je Regular, Medium, SemiBold, Bold), © 2017 IBM Corp. | SIL OFL 1.1 | Schrift der Oberfläche, eingebettet unter `src/AndiGenerator.UI/Assets/Fonts` mit `OFL.txt` |
| xunit | Apache-2.0 | nur Tests |
| xunit.runner.visualstudio | Apache-2.0 | nur Tests |
| Microsoft.NET.Test.Sdk | MIT | nur Tests |
| Avalonia.Headless | MIT | nur Tests (Oberflächentests ohne Bildschirm) |

## Werkzeuge nur zur Build-Zeit (werden nicht ausgeliefert)

Analyzer laufen nur beim Kompilieren und landen nicht in den Programmdateien (`PrivateAssets=all` über `GlobalPackageReference`). E11 betrifft sie daher nicht; sie sind trotzdem hier aufgeführt.

Geprüft vor der Veröffentlichung (01.10.2026): SonarAnalyzer.CSharp steht seit Version 10.33 unter der Sonar Source-Available License. Wir halten das für unproblematisch, weil der Analyzer ein reines Entwicklungswerkzeug ist – wie ein Compiler: Er wird beim Bauen von NuGet geladen, ist nicht Teil des Quellcodes im Repository und wird weder ausgeliefert noch signiert. E11 nimmt solche Werkzeuge seit der Präzisierung vom 01.10.2026 ausdrücklich von der Lizenzregel aus; die Bedingung der SignPath Foundation (kein proprietärer Code in den signierten Dateien) betrifft ebenfalls nur ausgelieferte Bestandteile. Wer das Projekt ohne Sonar bauen möchte, entfernt die Zeile in `Directory.Packages.props`; StyleCop (MIT) bleibt.

| Paket | Lizenz | Verwendung |
|---|---|---|
| StyleCop.Analyzers | MIT | Stilprüfung (Konfiguration: `stylecop.json`, `.editorconfig`) |
| anchore/sbom-action (GitHub Action, nutzt Syft) | Apache-2.0 | Stückliste (SBOM) im Release-Workflow |
| actions/attest-build-provenance, actions/attest-sbom (GitHub Actions) | MIT | Herkunftsnachweise im Release-Workflow |
| SonarAnalyzer.CSharp | Sonar Source-Available License v1 (kein OSI-Open-Source; bis 10.32 LGPL-3.0) | Qualitätsprüfung |

Geplant: Avalonia.Controls.DataGrid (MIT). **Nicht** zulässig: Avalonia TreeDataGrid/Accelerate, QuestPDF.
