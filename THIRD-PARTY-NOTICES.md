# Drittbibliotheken

Zulässig sind nur MIT-, Apache-2.0-, BSD- oder LGPL-lizenzierte Bibliotheken (MIGRATIONSPLAN E11). Jede neue Abhängigkeit wird hier eingetragen.

Ausnahme Schriften: Die SIL Open Font License (OFL 1.1) erlaubt das Einbetten und Weitergeben mit GPL-Software; der Lizenztext liegt neben den Schriftdateien, der reservierte Name „Plex“ wird nicht für veränderte Schriften verwendet.

| Paket | Version | Lizenz | Verwendung |
|---|---|---|---|
| .NET 10 Runtime/BCL | 10.0 | MIT | Laufzeit |
| Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent | 12.1.3 | MIT | Oberfläche (E2) |
| Avalonia.Skia (mit SkiaSharp, HarfBuzzSharp) | 12.1.3 | MIT | PDF-Ausdruck (`SKDocument`, Phase 7) |
| Dock.Avalonia, Dock.Avalonia.Themes.Fluent, Dock.Model.Mvvm | 12.1.0.6 | MIT | Andock-Layout der Ansichten (E15) |
| CommunityToolkit.Mvvm | 8.4.0 | MIT | ViewModels (E3) |
| Velopack | 0.0.1298 | MIT | Installation und Updates über GitHub Releases (E12) |
| Schriften IBM Plex Sans und IBM Plex Mono (je Regular, Medium, SemiBold, Bold) | 2017 IBM Corp. | SIL OFL 1.1 | Schrift der Oberfläche, eingebettet unter `src/AndiGenerator.UI/Assets/Fonts` mit `OFL.txt` |
| xunit | 2.9.3 | Apache-2.0 | nur Tests |
| xunit.runner.visualstudio | 3.1.0 | Apache-2.0 | nur Tests |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | nur Tests |

## Werkzeuge nur zur Build-Zeit (werden nicht ausgeliefert)

Analyzer laufen nur beim Kompilieren und landen nicht in den Programmdateien (`PrivateAssets=all` über `GlobalPackageReference`). E11 betrifft sie daher nicht; sie sind trotzdem hier aufgeführt.

| Paket | Version | Lizenz | Verwendung |
|---|---|---|---|
| StyleCop.Analyzers | 1.2.0-beta.556 | MIT | Stilprüfung (Konfiguration: `stylecop.json`, `.editorconfig`) |
| SonarAnalyzer.CSharp | 10.33.0.1635 | Sonar Source-Available License v1 (kein OSI-Open-Source; vor Veröffentlichung des Repositorys nochmals prüfen) | Qualitätsprüfung |

Geplant: Avalonia.Controls.DataGrid (MIT). **Nicht** zulässig: Avalonia TreeDataGrid/Accelerate, QuestPDF.
