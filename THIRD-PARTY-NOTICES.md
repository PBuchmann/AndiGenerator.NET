# Drittbibliotheken

Für alles, was mit dem Programm ausgeliefert oder im Quellcode als Bibliothek verwendet wird, sind nur MIT-, Apache-2.0-, BSD- oder LGPL-lizenzierte Pakete zulässig (MIGRATIONSPLAN E11). Reine Entwicklungswerkzeuge sind davon ausgenommen (siehe unten). Jede neue Abhängigkeit wird hier eingetragen. Die gültigen Versionen der NuGet-Pakete stehen in [`Directory.Packages.props`](Directory.Packages.props) (Dependabot hält sie aktuell), die der GitHub Actions in den Workflows unter `.github/workflows`.

Ausnahme Schriften: Die SIL Open Font License (OFL 1.1) erlaubt das Einbetten und Weitergeben mit GPL-Software; der Lizenztext liegt neben den Schriftdateien, der reservierte Name „Plex“ wird nicht für veränderte Schriften verwendet.

Ausnahme Daten: Die Postleitzahlen-Tabelle `src/AndiGenerator.Persistence/Geo/plz-de.csv.gz` (Postleitzahl, Ort, Breite, Länge) ist aus `DE.zip` von **GeoNames** (https://www.geonames.org, Postleitzahlen-Export https://download.geonames.org/export/zip/) abgeleitet: auf diese vier Spalten gekürzt und komprimiert (`tools/geonames/plz_tabelle.py`). Die Daten stehen unter **Creative Commons Attribution 4.0** (CC BY 4.0, https://creativecommons.org/licenses/by/4.0/); Namensnennung: „Postleitzahlen-Daten: GeoNames (geonames.org), CC BY 4.0, gekürzt“. Sie dienen nur der Entfernung in Luftlinie zwischen Hallen (Prüfung der 60-km-Regel) und sind keine Programmbibliothek.

Die Umrisse der Bundesländer `src/AndiGenerator.Persistence/Geo/umrisse-de.json` stammen aus **Natural Earth** (https://www.naturalearthdata.com, „Admin 1 – States, Provinces“, Maßstab 1:10 Mio.), vereinfacht mit `tools/karte/umrisse.py`. Natural Earth ist gemeinfrei (public domain); eine Namensnennung ist nicht nötig, steht aber in der Ansicht „Kompaktspieltage“. Die Lage der Städte in `staedte-de.json` ist aus der GeoNames-Tabelle oben abgeleitet.

| Paket | Lizenz | Verwendung |
|---|---|---|
| .NET 10 Runtime/BCL | MIT | Laufzeit |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent | MIT | Oberfläche (E2) |
| Avalonia.Skia (mit SkiaSharp, HarfBuzzSharp) | MIT | PDF-Ausdruck (`SKDocument`, Phase 7); SkiaSharp (mit SkiaSharp.NativeAssets.Linux) auch direkt im MCP-Server |
| Dock.Avalonia, Dock.Avalonia.Themes.Fluent, Dock.Model.Mvvm | MIT | Andock-Layout der Ansichten (E15) |
| CommunityToolkit.Mvvm | MIT | ViewModels (E3) |
| PdfPig (UglyToad.PdfPig) | Apache-2.0 | Bericht „Terminwünsche“ aus click-TT lesen (nur im Baustein `AndiGenerator.Import`) |
| HiGHS (Paket Highs.Native, © die HiGHS-Entwickler, University of Edinburgh) | MIT | Löser für die gezielte Konstruktion der Kompaktspieltage (ganzzahlige Optimierung, nur die native Bibliothek über die C-Schnittstelle, im Baustein `AndiGenerator.Import`; der Rechenkern kennt nur seine Schnittstelle); https://highs.dev |
| Velopack | MIT | Installation und Updates über GitHub Releases (E12); Version gemeinsam mit `VPK_VERSION` in `release.yml` |
| ModelContextProtocol (mit ModelContextProtocol.Core), © Anthropic und Mitwirkende | Apache-2.0 | MCP-Server `andigen-mcp` (`tools/AndiGenerator.Mcp`, liegt den Installationspaketen bei): Steuerung durch eine KI-Anwendung über stdio |
| Microsoft.Extensions.Hosting und Microsoft.Extensions.AI.Abstractions (mit weiteren Microsoft.Extensions-/System-Paketen) | MIT | Rahmen des MCP-Servers (`andigen-mcp`, liegt den Installationspaketen bei) |
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
| Microsoft.VisualStudio.Threading.Analyzers, © Microsoft | MIT | Prüfung von async/await und Threads (Konfiguration: `.editorconfig`) |
| anchore/sbom-action (GitHub Action, nutzt Syft) | Apache-2.0 | Stückliste (SBOM) im Release-Workflow |
| actions/attest-build-provenance, actions/attest-sbom (GitHub Actions) | MIT | Herkunftsnachweise im Release-Workflow |
| SonarAnalyzer.CSharp | Sonar Source-Available License v1 (kein OSI-Open-Source; bis 10.32 LGPL-3.0) | Qualitätsprüfung |

Geplant: Avalonia.Controls.DataGrid (MIT). **Nicht** zulässig: Avalonia TreeDataGrid/Accelerate, QuestPDF.
