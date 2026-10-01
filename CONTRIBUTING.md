# Mitwirken an AndiGenerator.NET

Schön, dass Sie mithelfen möchten! Fehler und Wünsche bitte als [Issue](https://github.com/PBuchmann/AndiGenerator.NET/issues) melden, Sicherheitslücken vertraulich nach [SECURITY.md](SECURITY.md).

## Ablauf

Auf `main` wird nicht direkt gepusht. Jede Änderung läuft über einen eigenen Branch und einen Pull Request:

1. **Branch anlegen** von einem aktuellen `main`, mit sprechendem Namen:
   - `feature/…` für neue Funktionen, z. B. `feature/update-schalter`
   - `fix/…` für Fehlerbehebungen, z. B. `fix/doppelklick-wunschtermin`
   - `doku/…` für reine Dokumentation
   - `einrichtung/…` für Build, Workflows und Repository-Einstellungen

   Ohne Schreibrechte im Repository: zuerst einen Fork anlegen und dort den Branch.
2. **Ändern, bauen, testen.** Unter Windows `bauen.cmd`, sonst `dotnet build AndiGenerator.slnx -c Release` und `dotnet test AndiGenerator.slnx -c Release --no-build`.
3. **Pull Request** gegen `main` öffnen und die Checkliste der Vorlage ausfüllen.
4. Auf GitHub laufen **Build und Tests** sowie **CodeQL** automatisch. Erst wenn beide grün sind und der Projektverantwortliche die Änderung freigegeben hat, wird sie mit **Squash and merge** übernommen.

Kleine, in sich abgeschlossene Pull Requests lassen sich schneller prüfen als große.

## Code-Stil

- **Warnungen sind Fehler** – auch die von StyleCop und SonarAnalyzer. Einstellungen in `.editorconfig` und `stylecop.json`.
- **Deutsche Fachbegriffe** für Klassen, Methoden und Variablen (`Mannschaft`, `Spieltag`, `Wunschtermin`), technische Begriffe englisch, wo sie üblich sind (`Command`, `ViewModel`). Kommentare und XML-Dokumentation auf Deutsch.
- **Schichten einhalten:** Domain ← Engine ← Persistence ← Application ← Presentation ← UI ← Desktop; Rendering zeichnet für Bildschirm und PDF. Die Presentation-Schicht kennt Avalonia nicht. Die Architekturtests prüfen das.
- **Referenzmodell:** `src/AndiGenerator.Engine/Referenz` ist eine bewusst wörtliche Portierung des Originals und dient als Maßstab der Paritätstests. Änderungen dort nur, wenn das Verhalten des Originals falsch übertragen wurde.
- **Fachliche Abweichungen vom Original** nur nach Absprache und als Eintrag im Migrationsplan (Abschnitt Befunde/Entscheidungen).

## Dateikopf und Lizenz

Beiträge stehen unter der [GPL-3.0-only](LICENSE) wie das ganze Projekt. Jede Quelldatei beginnt mit einem SPDX-Kopf ([REUSE](https://reuse.software)):

```csharp
// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only
```

Wer wesentlich zu einer Datei beiträgt, ergänzt eine eigene Zeile `SPDX-FileCopyrightText: <Jahr> <Name>`.

## Testdaten

Die Tests laufen gegen die **anonymisierten** Testdaten in `testdaten/` (siehe [Doku/TESTDATEN.md](Doku/TESTDATEN.md)). Echte click-TT-Exporte enthalten Vereins- und Hallennamen – bitte **nie** ungeprüft einchecken, auch nicht in Issues oder Pull Requests anhängen. Neue Testfälle mit `tools/anonymisieren` anonymisieren oder von Hand erfundene Namen verwenden. Ebenso tabu: persönliche Pfade, E-Mail-Adressen und Zugangsdaten.

## Dokumentation

- Bedienung geändert → [Doku/ANLEITUNG.md](Doku/ANLEITUNG.md) anpassen; die PDF erzeugt der Projektverantwortliche neu (`tools/anleitung`).
- Fachliche Änderungen und Entscheidungen → Änderungsprotokoll in [Doku/MIGRATIONSPLAN.md](Doku/MIGRATIONSPLAN.md).
