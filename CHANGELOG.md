# Änderungen

Was sich von Version zu Version für Anwender ändert. Die Einzelheiten stehen in der [Anleitung](Doku/ANLEITUNG.md), die technische Geschichte im [Migrationsplan](Doku/MIGRATIONSPLAN.md).

## Unveröffentlicht

### Geändert

- **Download einfacher:** Oben im Release steht, welche Datei man für welchen Rechner braucht; README und Anleitung haben dieselbe Tabelle mit festen Links auf die jeweils neueste Version. Dafür tragen die Dateien für Mac und Linux keine Versionsnummer mehr im Namen.

## 0.9.6 (12.10.2026)

### Neu

- **Absturzschutz:** Ein unerwarteter Fehler beendet das Programm nicht mehr still. Der aktuelle Plan wird als gemerkter Plan „Notfallsicherung“ abgelegt, die Einzelheiten kommen in `fehlerprotokoll.txt` im Datenordner, und eine Meldung sagt, wo sie liegen. Das gilt auch, wenn eine laufende Generierung abbricht.
- Die Oberfläche ist auch auf einem anders eingestellten System deutsch (Monatsnamen, Zahlen, Datumsformate); leere Zeitfelder zeigen „Stunde“ und „Minute“.
- **Leitstand** (vorher Maschinenraum): Rechenkerne, Schaubild Inseln → Koordinator → bester Plan mit einem grünen Punkt je Verbesserung und einem Kasten, was sich geändert hat; im Automodus die angehobenen Gewichte.
- **Verlauf:** Kosten bzw. Verstöße A, B und C seit dem Start, mit den Phasen des Automodus.
- **Gesicherter Plan:** Bevor ein Plan durch einen Neustart oder das Schließen verloren ginge, wird er gesichert bzw. mit dem bisher gesicherten verglichen und nachgefragt.
- **Spielplandaten:** Kalender der spielfreien Tage mit Grund im Tooltip, Wunschtermine als Kärtchen mit Ziehen und Ablegen (Strg kopiert) und Vorschlag der üblichen Uhrzeit, Setzliste per Ziehen, einheitliche Knöpfe neben den Listen, Marken und Änderungspunkte in der Seitenliste.

### Geändert

- **Fehler des Originals korrigiert:** Ein Wunschtermin gilt beim Zurücksetzen eines Spiels nicht mehr fälschlich als frei; beim Füllen über die Auswärtskoppel wird kein schon terminiertes Spiel mehr umgesetzt; das Startraster legt bei ungerader Mannschaftszahl alle Spieltage an; beim letzten Spieltag zählen alle Überlappungen (die Kosten können dadurch etwas höher sein als im alten AndiGenerator).
- **Pflichtspieltag bei ungerader Mannschaftszahl:** Die zwangsläufig spielfreie Mannschaft wird nicht mehr bestraft, wenn sie in der Woche davor spielt (abschaltbar in den Einstellungen).
- Kopfzeile: ▶ Start / ⏸ Pausieren und ✕ Schließen / ■ Stopp; das Verfahren wird mit zwei Segmentknöpfen gewählt.
- Die Frage „nur Rückrunde planen?“ nennt den Beginn der Rückrunde.

## 0.9.5 (10.10.2026)

### Neu

- Version für Macs (Apple-Chip und Intel) und für Windows auf ARM (Snapdragon).

## 0.9.4 (05.10.2026)

### Neu

- **Automodus:** Die Suche lenkt sich selbst über die Gewichte – erst Pflichtregeln (Stufe A), dann Wünsche (B), dann die übrige Güte (C).
- Pakete für Chromebooks und Linux; die Bildschirmfotos der Anleitung entstehen automatisch.

## 0.9.3 (01.10.2026)

Erste öffentliche Beta: vollständige Portierung des AndiGenerators von Andreas Hofmann nach C#/.NET 10 mit neuer Oberfläche – Optimierung und Bewertung wie im Original, alle Ansichten, Spielplandaten, Export für click-TT, Druck und Excel, Installation und Updates über GitHub Releases.
