# Änderungen

Was sich von Version zu Version für Anwender ändert. Die Einzelheiten stehen in der [Anleitung](Doku/ANLEITUNG.md), die technische Geschichte im [Migrationsplan](Doku/MIGRATIONSPLAN.md).

## Unveröffentlicht

### Neu

- **Kompaktspieltage** (z. B. Jugend-Verbandsoberliga): Bei einer Halbrunde mit überwiegend Spieltagen am Wochenende fragt die Einrichtung, ob die Staffel in 3er- und 4er-Spieltagen geplant wird. Die neue Ansicht prüft den Plan aus click-TT gegen die Meldungen der Vereine aus den Terminwünschen: Raster aus Meldungen und Plan, Karte der Hallen mit den Fahrten (Luftlinie), km je Mannschaft, Spieltage mit den in click-TT zu verlegenden Hallen und Hinweise.
- **Kompaktspieltage planen:** Im Kompaktmodus berechnet ▶ Varianten aus 4er-Spieltagen (3er nur, wenn es anders nicht aufgeht; ausgewogen, kürzeste Fahrten in Luftlinie, gleichmäßiger Stand, gemeinsamer Spieltag); die gewählte gilt für Terminplan, Druck und Export. Die Kennzahlen zeigen dann Fahrten, Meldungen, Heimspieltage und Spieltage; Kosten-, Qualitäts- und Meldungsansicht sowie die Wahl des Verfahrens entfallen. Beim 4er-Spieltag lautet das vierte Spiel Gast 1 – Partner, so ist der Partner nur einmal formal Heimmannschaft. Der Dialog *Plan sichern* vergleicht im Kompaktmodus die Kennzahlen der Kompaktspieltage statt Stufen und Kosten.
- **Bestes Gesamtpaket:** Der Kompaktplaner wählt unter den kürzesten reinen 4er-Plänen den, der Fahrten, gleich weite Fahrten für alle Mannschaften, Stand, gemeinsamen Spieltag sowie Spitzen- und Abstiegsspieltag zusammen am besten trifft. Kompaktspieltage auch samstags (mit Heimtermin), Einzelbegegnungen auch unter der Woche bis 60 km; Einzel-Heimspiele zählen bei den Heimspieltagen mit. Die Planung lässt sich anhalten (zeigt die bisher besten Pläne), fortsetzen und beenden. Haben alle Pläne Verstöße, sucht der Planer vollständig nach einem Plan ganz ohne Verstoß – notfalls mit einer Mannschaft ohne Heimspieltag oder mit Einzelspielen. Varianten mit 3er-Spieltagen (höchstens drei) nur, wenn sie ein Ziel besser treffen als jeder reine 4er-Plan; die Erläuterung nennt die Punkte jedes Plans mit Begründung. Was es schon mindestens ebenso gut erfüllt, steht auf seiner Karte („✓ zugleich“) statt als eigene Variante. Neue Variante *Gleich weite Fahrten*; Fortschrittsbalken über die ganze Breite während der Berechnung.
- **Kompaktspieltage gezielt konstruieren statt zufällig suchen:** Der Planer stellt die Pläne mit dem freien Löser HiGHS (MIT-Lizenz) als ganzzahlige Optimierung auf – jeder mögliche Spieltag an jedem möglichen Tag ist eine Ja/Nein-Entscheidung – und findet so den kürzesten Plan und den mit den wenigsten Punkten nachweisbar, auch bei knappen Meldungen (Testklasse 10er eng: rund 17 % kürzer als bisher). Dazu ein Plan mit Spitzen- und Abstiegsspieltag (wenn ihn das Gesamtpaket nicht schon hat) und zwei weitere Pläne mit anderen Spieltagen zur Auswahl. Unabhängige Schritte rechnen gleichzeitig, und jeder Schritt lässt mehrere Läufe des Lösers mit verschiedenen Startwerten parallel rechnen; der erste, der den besten Plan beweist, beendet die anderen. Die Zufallssuche liefert die Startpunkte und läuft danach nur noch kurz als Ergänzung.
- **Gespeicherte Pläne im Kompaktmodus:** Der gesicherte und die gemerkten Pläne stehen in der Ansicht „Kompaktspieltage“ unter „Pläne“ neben den Varianten, mit Punkten und Kennzahlen. Gewählt gelten sie mit genau den gespeicherten Spielen (Uhrzeiten, Hallen, Heimrecht) für Terminplan, Druck und den Export für click-TT. Den gewählten berechneten Plan merkt man direkt auf seiner Karte („Merken …“). Vor dem Export für click-TT prüft der Kompaktmodus den Plan mit den Hinweisen der Kompaktauswertung und listet sie auf, statt mit der Prüfung für normale Pläne (die jedes Spiel außerhalb eines Heimtermins der Heimmannschaft als ungültig zählt). Die CSV enthält dabei das Spiellokal (Nummer der Halle) für die Spiele des Ausrichters – das seines Heimtermins an diesem Tag, sonst sein Standardlokal; bisher blieb es leer, weil nur ein Heimtermin mit genau der Uhrzeit des Spiels zählte. Spiele anderer Heimmannschaften bleiben ohne Spiellokal und werden in click-TT in die Halle des Ausrichters verlegt. Gesicherte und gemerkte Pläne speichern das Spiellokal mit (nur wenn es eins gibt; Pläne ohne bleiben wie im Original), so bleibt beim 4er-Spieltag der Ausrichter beim Wiederöffnen derselbe wie berechnet.
- **Übersicht der Kompaktspieltage für die Vereine (Drucken):** Im Kompaktmodus bietet die Druckauswahl statt der Kosten „Kompaktspieltage (Übersicht für die Vereine)“. Jeder Teil beginnt auf einer neuen Seite: Klassenname mit dem Raster „Meldungen und Plan“ samt Legende, Karte aller Fahrten mit „km je Mannschaft“, Spieltage und je Mannschaft genau eine Seite mit der Karte ihrer Fahrten; Hinweise für die Staffelleitung (zu verlegende Hallen, Verstöße) stehen nicht darin. Ansicht und Ausdruck zeichnen die Karte mit demselben Kartenzeichner. Die Namen der Hallen und Städte werden dabei mit ihrer tatsächlichen Breite gesetzt: ganz im Bild und ohne sich zu überdecken (bisher wurden Namen am Rand abgeschnitten, z. B. „Klein-Winternheim“); Städte entfallen, wo kein Platz ist.
- **Meldungen im Raster erfassen:** Mit dem Schalter *Bearbeiten* über dem Raster „Meldungen und Plan“ steht ein Rahmen auf einem Feld (Klick oder Pfeiltasten); **A** auswärts, **H** nur Heim, **B** beides, **Entf** bzw. **Leertaste** kann nicht. Für Heimtermine lassen sich Beginn (leer = Standard: 3er-Spieltag 10:00, 4er-Spieltag 11:00; eine eingetragene Uhrzeit gilt so, wie sie ist), Halle und zwei Tische (4er-Spieltag) einstellen, die Halle auf Wunsch für alle Heimtermine der Mannschaft; der Tooltip eines Felds zeigt Halle und Beginn; weitere Tage werden als Zeilen angelegt (alle Felder zunächst leer). Gespeichert wird sofort wie in den Spielplandaten: Heimtermin, Sperrtermin bzw. die Zeile „Nur Heim: …“ in der Bemerkung. Samstage gelten dabei wie jeder andere Tag (auswärts, wenn nicht gesperrt), nicht mehr wie der Sonntag desselben Wochenendes.
- **Spitzen- und Abstiegsspieltag:** Mit bekanntem QTTR-Schnitt sucht der Kompaktplaner zusätzlich den kürzesten Plan, in dem die vier stärksten und die vier schwächsten Mannschaften je einen 4er-Spieltag bilden, so spät wie möglich; in Plänen aus 3er-Spieltagen zählt ein 3er-Spieltag der drei Stärksten bzw. Schwächsten.
- **Bericht „Terminwünsche“ (PDF) ist bei Klassen für Kompaktspieltage Pflicht:** Ohne ihn lässt sich die Einrichtung nicht abschließen (Hallenadressen für Karte und Entfernungen).
- **Klassen mit „(HR)“ bzw. „(FR)“ im Namen** spielen nur eine Halbrunde: Geplant wird ohne Rückfrage nur die Hinrunde („(HR)“) bzw. nur die Rückrunde („(FR)“); in den Einstellungen ist die Rundenplanung dann fest.
- **Setzliste mit TTR-Werten:** Die Schnitte aus der Mannschaftsmeldung werden mit der Staffel gespeichert und in der Setzliste neben jeder Mannschaft gezeigt; **Nach TTR ordnen** stellt die Reihenfolge danach auf.

### Geändert

- **Kompaktspieltage, Raster und Karte:** Die Begegnungen des Spitzenspieltags haben im Raster einen schwarzen, die des Abstiegsspieltags einen blauen Ring. In der Karte ist bei einer Auswahl der Ausgangspunkt grün und jedes Ziel in der Farbe seiner Linie (Einzelbegegnung gelb), bei zwei verschiedenfarbigen Linien geteilt in Halbkreise; Pfeilspitzen zeigen die Richtung, mehrere Fahrten zwischen denselben Hallen liegen nebeneinander, und sehr kurze Fahrten (z. B. Konz – Trier) liegen als Pfeil mit hellem Rand über den Hallenpunkten. Ohne Plan entfallen „km je Mannschaft“ und die Hinweise auf fehlende Paarungen.
- **Kompaktspieltage, Raster:** Ausrichter (★) und Gäste (●) waren nach der Umstellung auf zentrierte Formen unsichtbar (Füllfarbe griff nicht) – behoben.
- **Anleitung:** Abschnitt 6.8 „Kompaktspieltage“ neu gefasst (Berechnung mit HiGHS, Pläne und Punkte, Raster und Karte, Meldungen bearbeiten, Druck und Export) mit fünf neuen Bildschirmfotos aus einer erfundenen Testklasse.
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
