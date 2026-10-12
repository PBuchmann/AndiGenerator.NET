# Änderungen

Was sich von Version zu Version für Anwender ändert. Die Einzelheiten stehen in der [Anleitung](Doku/ANLEITUNG.md), die technische Geschichte im [Migrationsplan](Doku/MIGRATIONSPLAN.md).

## Unveröffentlicht

### Neu

- **Kompaktspieltage** (z. B. Jugend-Verbandsoberliga): Bei einer Halbrunde mit überwiegend Spieltagen am Wochenende fragt die Einrichtung, ob die Staffel in 3er- und 4er-Spieltagen geplant wird. Die neue Ansicht prüft den Plan aus click-TT gegen die Meldungen der Vereine aus den Terminwünschen: Raster aus Meldungen und Plan, Karte der Hallen mit den Fahrten (Luftlinie), km je Mannschaft, Spieltage mit den in click-TT zu verlegenden Hallen und Hinweise.
- **Kompaktspieltage planen:** Im Kompaktmodus berechnet ▶ Varianten aus 4er-Spieltagen (3er nur, wenn es anders nicht aufgeht; ausgewogen, kürzeste Fahrten in Luftlinie, gleichmäßiger Stand, gemeinsamer Spieltag); die gewählte gilt für Terminplan, Druck und Export. Spieltage lassen sich festhalten, Fahrtwünsche („Sinzig spielt in Trier“) vorgeben. Die Kacheln zeigen dann Fahrten, Meldungen, Heimspieltage und Spieltage; Kosten-, Qualitäts- und Meldungsansicht sowie die Wahl des Verfahrens entfallen.
- **Bericht „Terminwünsche“ (PDF) ist bei Klassen für Kompaktspieltage Pflicht:** Ohne ihn lässt sich die Einrichtung nicht abschließen (Hallenadressen für Karte und Entfernungen).
- **Klassen mit „(HR)“ bzw. „(FR)“ im Namen** spielen nur eine Halbrunde: Geplant wird ohne Rückfrage nur die Hinrunde („(HR)“) bzw. nur die Rückrunde („(FR)“); in den Einstellungen ist die Rundenplanung dann fest.
- **Setzliste mit TTR-Werten:** Die Schnitte aus der Mannschaftsmeldung werden mit der Staffel gespeichert und in der Setzliste neben jeder Mannschaft gezeigt; **Nach TTR ordnen** stellt die Reihenfolge danach auf.

### Geändert

- Der Knopf **Fortsetzen** in der Pause hat ein eigenes Symbol (Strich und Play) und unterscheidet sich so von **Start**.
- **Leitstand:** Die Säulen der Rechenkerne füllen die ganze Höhe; die helle Spur zeigt 100 % (so schnell wie der schnellste Kern). Bisher sah jeder Kern nur halb ausgelastet aus.

## 0.9.7 (12.10.2026)

### Neu

- **Daten aus click-TT ergänzen** (erster Punkt der Einrichtung): Die **Mannschaftsmeldung** (CSV) ergibt aus den TTR-Werten der Stammspieler einen Vorschlag für die Setzliste. Der Bericht **Terminwünsche** (PDF) liefert die Spiellokal-Nummern für den Export, die Adressen der Hallen und die Bemerkungen der Vereine und zeigt Unterschiede zu den Spielplandaten. Erledigt ist der Punkt, wenn beide Dateien übernommen sind – auch in verschiedenen Sitzungen. Die Einrichtung lässt sich abbrechen.
- **Prüfung der 60-km-Regel:** Entfernung der Hallen in Luftlinie (aus Postleitzahl und Ort, deutschlandweit und ohne Internet, oder aus der GPS-Angabe des Vereins). Hinweise „Sperre fehlt vermutlich“ und „Sperre vermutlich unnötig“ für Gastgeber mit Heimterminen unter der Woche und am Wochenende; fehlende Sperren lassen sich mit einer Rückfrage setzen, ein Protokoll zeigt welche.
- Seite **Wochenende (60-km-Regel)**: Entfernung je Gegner, Marken „weit“ und „nah“ und die Summe aller Entfernungen.
- **Hallenbelegung nach Hallennamen:** Ein Heimspiel einer Nachbarmannschaft in einer anderen Halle belegt die eigene Halle nicht mehr.

### Geändert

- Nach dem Laden einer Datei erscheint eine deutlich sichtbare Meldung direkt unter dem Knopf.
- In den Kacheln *Verbesserungen* und *Berechnete Pläne* steht „Details ›“ wie in der Ergebniskachel oben rechts.
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
