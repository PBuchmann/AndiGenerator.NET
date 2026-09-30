# Entstehung von AndiGenerator.NET

AndiGenerator.NET ist die C#/.NET-Portierung des *AndiGenerator* von Andreas Hofmann. Der eigene Name (E16) grenzt sie vom Original ab; die Herkunft bleibt im Namen erkennbar.

## Beteiligte

- **Andreas Hofmann** – Autor des Originals *AndiGenerator* (Delphi, Copyright 2017 ff., GPL-3.0). Idee und Herzstück stammen aus seinem Programm: die Planungslogik, die Optimierung mit mehreren „Inseln“, die Kostenfunktion mit ihren Kostenarten und Gewichtungen, der click-TT-Import mit Änderungsdateien, die Auswertung von Terminwünschen und Nachbarmannschaften, die Diagramme und die Dateiformate. Die Oberfläche wurde für AndiGenerator.NET neu gestaltet; Funktionsumfang und Ergebnisse entsprechen dem Original.
- **Peter Buchmann** – Initiator und Verantwortlicher der Portierung (Copyright 2026 an den Änderungen). Er hat die Entscheidungen getroffen (Plattformen, UI-Framework, Umgang mit Fehlern des Originals), die Referenzdaten mit dem Original erzeugt, jede Stufe gebaut, getestet und abgenommen.
- **Claude (Anthropic)** – KI-Assistent. Er hat den Delphi-Quelltext analysiert, den Migrationsplan und die Einzeldokumentation geschrieben und den C#-Code samt Tests nach Peters Vorgaben umgesetzt.

## Vorgehen

1. **Analyse des Originals.** Jede Delphi-Unit wurde einzeln beschrieben (`Doku/Dateien/`), Auffälligkeiten und Fehler wurden gesammelt (MIGRATIONSPLAN, Abschnitt 6).
2. **Entscheidungen.** Plattformen, Framework (Avalonia), Architektur, Parallelisierung, Dateiformate, Umgang mit Fehlern (E9: Abstürze und Bedienfehler beheben, Verhalten der Kostenfunktion 1:1 übernehmen) und Lizenz wurden vorab festgelegt und protokolliert (MIGRATIONSPLAN, Abschnitt 4).
3. **Referenzdaten.** Echte click-TT-Dateien wurden mit dem Original verarbeitet; Kosten, Pläne, CSV-Exporte, `.modifications` der Datendialoge und Ausdrucke wurden als Sollwerte festgehalten (`Referenz/`, nicht öffentlich). Für das öffentliche Repository sind die Testdaten anonymisiert (`testdaten/`, `tools/anonymisieren`, siehe `Doku/TESTDATEN.md`).
4. **Umsetzung in Phasen** (MIGRATIONSPLAN, Abschnitt 5): Fachmodell und Dateien, Kostenfunktion und Optimierung, parallele Engine, Oberfläche, Ansichten, Datendialoge, Druck und Export. Jede Phase endet erst, wenn ihre Paritätstests grün sind: gleiche Kosten wie das Original für alle Referenzpläne, byte- bzw. inhaltsgleiche Dateien.
5. **Qualität.** Jeder Build läuft mit StyleCop- und Sonar-Analyzern, deren Warnungen als Fehler gelten, und mit allen Tests; die Testabdeckung wird regelmäßig gemessen.

## Abweichungen vom Original

Behobene Fehler und bewusste Abweichungen (z. B. PDF statt direktem Druck, Seitenumbruch zwischen Zeilen, korrigierte Tippfehler) stehen mit Begründung im MIGRATIONSPLAN (Abschnitt 6 und Änderungsprotokoll). Alles Übrige verhält sich wie das Original.
