# Arbeitsregeln für Claude in diesem Repository

AndiGenerator.NET – Portierung des AndiGenerator (Delphi) nach C#/.NET 10 mit Avalonia 12. Ansprechpartner: Peter
(spricht Deutsch; Antworten, Code-Bezeichner, Kommentare und Doku auf Deutsch).

Zu Beginn jeder Sitzung auch `Doku/PROJEKTWISSEN.md` lesen (Arbeitsweise, Vorlieben, Kompaktspieltage, offene Punkte).

## Bauen und Testen
- Vor dem Bauen prüfen, dass `AndiGenerator.NET.exe` nicht läuft (sonst schlägt der Build fehl) – ggf. Peter bitten, sie zu schließen.
- Bauen wie `bauen.cmd`: `dotnet build AndiGenerator.slnx -c Release -nologo -v:minimal` (immer die ganze Solution).
- Testen gezielt: Bei lokalen Änderungen nur die Tests, die betroffen sein können – betroffenes Testprojekt und
  `--filter "FullyQualifiedName~Klasse1|FullyQualifiedName~Klasse2"` (z. B. Karte: `Kartenbau|Kompaktdruck`). Den vollen
  Lauf (`dotnet test AndiGenerator.slnx -c Release --no-build -nologo --filter "Kategorie!=Messung"`, gut 2 Minuten, davon
  fast alles Persistence.Tests) vor jedem Commit und wenn Peter danach fragt. Im Bericht sagen, was gelaufen ist.
- StyleCop-, Sonar-, .NET-Analyzer- (AnalysisMode Recommended) und VS-Threading-Befunde sind Build-Fehler. Häufig: SA1515 (Leerzeile vor Kommentar), SA1513 (Leerzeile nach `}`),
  SA1636 (Dateikopf, siehe unten), SA1201/SA1202/SA1204 (Reihenfolge der Member), S3358 (keine verschachtelten
  Bedingungsausdrücke), S3267 (Schleifen mit Bedingung → LINQ), S1117/CS0136 (Namen verdecken), S2234 (Argumentreihenfolge),
  S3604 (überflüssiger Initialisierer). Erst melden, wenn der Build grün ist.
- Zeitabhängige Tests (HiGHS, Kompaktplaner) nicht durch lange Zeitgrenzen „reparieren“, wenn die Suche dann nicht von selbst
  früher endet – sonst dauert der Testlauf Minuten.
- Keine automatischen Leistungsmessungen mit dem Original-AndiGenerator.

## Dateikopf (jede .cs-Datei)
```
// SPDX-FileCopyrightText: 2017 Andreas Hofmann
// SPDX-FileCopyrightText: 2026 Peter Buchmann
// SPDX-License-Identifier: GPL-3.0-only
```

## Architektur
- Schichten: Domain, Engine (ohne Paketabhängigkeiten – Architekturtest), Import, Persistence, Application, Rendering,
  Presentation (ViewModels ohne Avalonia), UI (Avalonia, Skia, Velopack).
- HiGHS: Schnittstelle `IGanzzahlloeser` in der Engine, `HighsLoeser` in Import, `Highsbruecke` in Application.
- Karte: `Kartenzeichner` (Presentation, auf `IZeichenflaeche`) zeichnet für Ansicht und PDF.

## Daten und Datenschutz
- `Referenz/` ist privat (gitignored), echte Daten mit Klarnamen. Nie ins Repository, nie in Commits.
- `testdaten/` ist anonymisiert; neue Testdaten nur anonymisiert oder erfunden (z. B. `testdaten/kompakt`).
- PDFs/CSVs mit personenbezogenen Daten bleiben in `Referenz/`.
- Die Originale in `%LOCALAPPDATA%\Andi-Generator` nie verändern (nur lesen/kopieren).
- Keine Windows-Sicherheitseinstellungen ändern.

## Git
- Gearbeitet wird auf Feature-Branches; Peter führt sie per Pull Request nach `main` zusammen.
- Commits nur nach grünem Build, möglichst klein und thematisch.
- Autor: `Peter Buchmann <PBuchmann@users.noreply.github.com>`.
- Commit-Nachricht auf Deutsch, am Ende:
  ```
  Co-Authored-By: Claude <noreply@anthropic.com>
  ```
- Nicht pushen und keine Zugangsdaten eingeben – Peter pusht selbst (Visual Studio).

## Doku und Versionen
- Jede sichtbare Änderung in `CHANGELOG.md` unter „Unveröffentlicht“ (aktuell Arbeit an 0.9.9; 0.9.8 ist veröffentlicht –
  den Abschnitt „Unveröffentlicht“ bei der ersten Änderung wieder anlegen). Release: Abschnitt zu `## x.y.z (Datum)`,
  PDF der Anleitung neu, Pull Request; nach dem Merge setzt Peter den Tag `vx.y.z` auf `main` (startet den Release-Workflow).
- Anleitung: `Doku/ANLEITUNG.md`; Bilder mit `bildschirmfotos.cmd` (Test `Bildschirmfotos`), PDF siehe `tools/anleitung/README.md`.
  `Doku/Anleitung.pdf` nur vor einem Pull Request neu erzeugen (`anleitung.cmd`), nicht bei jeder Änderung der Anleitung –
  bis dahin darf das PDF hinter `ANLEITUNG.md` zurückliegen.
- Entscheidungen und offene Punkte: `Doku/MIGRATIONSPLAN.md` (Kompaktspieltage: Abschnitt 12).
- Signierung darf nichts kosten.

## Oberfläche
- Explorer (Windows) nur per Klick bedienen lassen, keine Skripte für Dateiverwaltung an Peter.
